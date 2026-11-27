using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Switchly.App.Common;

/// <summary>
/// Lightweight local-cached favicon fetcher for Web Workspace Groups (D10).
/// Treats downloaded bytes as untrusted input (size-capped, timeout-bounded, frozen BitmapImage,
/// manual redirect validation + DNS-rebinding-safe ConnectCallback) and gracefully returns null on failure.
/// </summary>
public static partial class FaviconCacheService
{
    private const int MaxIconBytes = 256 * 1024;
    private const int MaxHtmlProbeBytes = 64 * 1024;
    private const int MaxRedirects = 3;

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(4),
            ConnectCallback = SafePublicConnectAsync,
        };

        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        return client;
    }

    private static async ValueTask<Stream> SafePublicConnectAsync(SocketsHttpConnectionContext context, CancellationToken ct)
    {
        var host = context.DnsEndPoint.Host;
        if (IsBlockedInternalHost(host))
        {
            throw new HttpRequestException("Blocked internal host.");
        }

        var addresses = await Dns.GetHostAddressesAsync(host, ct).ConfigureAwait(false);
        if (addresses.Length == 0 || addresses.Any(IsBlockedIpAddress))
        {
            throw new HttpRequestException("Blocked private/loopback DNS resolution.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses[0], context.DnsEndPoint.Port, ct).ConfigureAwait(false);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    private static readonly ConcurrentDictionary<string, ImageSource?> MemoryCache = new(StringComparer.OrdinalIgnoreCase);

    private static readonly string CacheDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SwitchlyNext",
        "Data",
        "Favicons");

    public static async Task<ImageSource?> GetFaviconForUrlAsync(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) ||
            !Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrWhiteSpace(uri.DnsSafeHost))
        {
            return null;
        }

        var host = uri.DnsSafeHost.ToLowerInvariant();
        if (IsBlockedInternalHost(host))
        {
            return null;
        }

        if (MemoryCache.TryGetValue(host, out var cached) && cached is not null)
        {
            return cached;
        }

        var effectiveUrl = string.Equals(host, "flow.google", StringComparison.OrdinalIgnoreCase)
            ? "https://flow.google.com"
            : url;
        var effectiveHost = Uri.TryCreate(effectiveUrl, UriKind.Absolute, out var effUri) && !string.IsNullOrWhiteSpace(effUri.DnsSafeHost)
            ? effUri.DnsSafeHost.ToLowerInvariant()
            : host;

        var safeFileName = SafeFileRegex().Replace(effectiveHost, "_") + ".png";
        var cachePath = Path.Combine(CacheDirectory, safeFileName);

        if (File.Exists(cachePath))
        {
            var localImage = TryDecodeFrozenBitmap(cachePath);
            if (localImage is not null)
            {
                MemoryCache[host] = localImage;
                return localImage;
            }
        }

        var downloaded = await TryFetchFaviconCandidatesAsync(effectiveUrl, effectiveHost);
        if (downloaded is null)
        {
            var redirectedHost = await TryResolveRedirectedHostAsync(effectiveUrl);
            if (!string.IsNullOrWhiteSpace(redirectedHost) &&
                !IsBlockedInternalHost(redirectedHost) &&
                !string.Equals(redirectedHost, effectiveHost, StringComparison.OrdinalIgnoreCase))
            {
                downloaded = await TryFetchFaviconCandidatesAsync($"https://{redirectedHost}", redirectedHost);
            }
        }

        if (downloaded is null)
        {
            return null;
        }

        var decoded = TryDecodeFrozenBitmap(downloaded);
        if (decoded is null)
        {
            return null;
        }

        try
        {
            Directory.CreateDirectory(CacheDirectory);
            await File.WriteAllBytesAsync(cachePath, downloaded);
        }
        catch
        {
        }

        MemoryCache[host] = decoded;
        return decoded;
    }

    private static async Task<byte[]?> TryFetchFaviconCandidatesAsync(string fullUrl, string host)
    {
        var htmlIconUrl = await TryExtractHtmlIconUrlAsync(fullUrl);
        if (!string.IsNullOrWhiteSpace(htmlIconUrl))
        {
            var fromHtml = await TryDownloadBytesAsync(htmlIconUrl);
            if (fromHtml is not null)
            {
                return fromHtml;
            }
        }

        return await TryDownloadBytesAsync($"https://www.google.com/s2/favicons?sz=64&domain_url={Uri.EscapeDataString(fullUrl)}")
            ?? await TryDownloadBytesAsync($"https://www.google.com/s2/favicons?sz=64&domain={Uri.EscapeDataString(host)}")
            ?? await TryDownloadBytesAsync($"https://icons.duckduckgo.com/ip3/{Uri.EscapeDataString(host)}.ico")
            ?? await TryDownloadBytesAsync($"https://{host}/favicon.ico");
    }

    private static async Task<HttpResponseMessage?> SendFollowingSafeRedirectsAsync(HttpMethod method, string initialUrl)
    {
        if (!Uri.TryCreate(initialUrl, UriKind.Absolute, out var currentUri) ||
            (currentUri.Scheme != Uri.UriSchemeHttp && currentUri.Scheme != Uri.UriSchemeHttps) ||
            IsBlockedInternalHost(currentUri.DnsSafeHost))
        {
            return null;
        }

        for (var hop = 0; hop <= MaxRedirects; hop++)
        {
            using var req = new HttpRequestMessage(method, currentUri);
            var resp = await Http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);

            var status = (int)resp.StatusCode;
            if (status is 301 or 302 or 303 or 307 or 308 && resp.Headers.Location is { } loc)
            {
                resp.Dispose();
                if (!Uri.TryCreate(currentUri, loc, out var nextUri) ||
                    (nextUri.Scheme != Uri.UriSchemeHttp && nextUri.Scheme != Uri.UriSchemeHttps) ||
                    IsBlockedInternalHost(nextUri.DnsSafeHost))
                {
                    return null;
                }

                currentUri = nextUri;
                if (status == 303 && method != HttpMethod.Head)
                {
                    method = HttpMethod.Get;
                }

                continue;
            }

            return resp;
        }

        return null;
    }

    private static async Task<string?> TryExtractHtmlIconUrlAsync(string pageUrl)
    {
        try
        {
            using var response = await SendFollowingSafeRedirectsAsync(HttpMethod.Get, pageUrl);
            if (response is null || !response.IsSuccessStatusCode)
            {
                return null;
            }

            var baseUri = response.RequestMessage?.RequestUri ?? new Uri(pageUrl);
            await using var stream = await response.Content.ReadAsStreamAsync();
            var buffer = new byte[MaxHtmlProbeBytes];
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(totalRead, buffer.Length - totalRead));
                if (read == 0)
                {
                    break;
                }

                totalRead += read;
            }

            if (totalRead == 0)
            {
                return null;
            }

            var html = System.Text.Encoding.UTF8.GetString(buffer, 0, totalRead);
            foreach (Match linkMatch in IconLinkTagRegex().Matches(html))
            {
                var tag = linkMatch.Value;
                var hrefMatch = HrefAttrRegex().Match(tag);
                if (!hrefMatch.Success)
                {
                    continue;
                }

                var rawHref = WebUtility.HtmlDecode(hrefMatch.Groups[1].Value.Trim());
                if (string.IsNullOrWhiteSpace(rawHref) || rawHref.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Uri.TryCreate(baseUri, rawHref, out var resolved) &&
                    (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps) &&
                    !IsBlockedInternalHost(resolved.DnsSafeHost))
                {
                    return resolved.AbsoluteUri;
                }
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private static async Task<string?> TryResolveRedirectedHostAsync(string url)
    {
        try
        {
            using var response = await SendFollowingSafeRedirectsAsync(HttpMethod.Head, url);
            return response?.RequestMessage?.RequestUri?.DnsSafeHost?.ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static async Task<byte[]?> TryDownloadBytesAsync(string iconUrl)
    {
        try
        {
            using var response = await SendFollowingSafeRedirectsAsync(HttpMethod.Get, iconUrl);
            if (response is null || !response.IsSuccessStatusCode)
            {
                return null;
            }

            if (response.Content.Headers.ContentLength is > MaxIconBytes)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(chunk)) > 0)
            {
                if (buffer.Length + read > MaxIconBytes)
                {
                    return null;
                }

                buffer.Write(chunk, 0, read);
            }

            return buffer.Length > 0 ? buffer.ToArray() : null;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? TryDecodeFrozenBitmap(string filePath)
    {
        try
        {
            var bytes = File.ReadAllBytes(filePath);
            return bytes.Length is > 0 and <= MaxIconBytes
                ? TryDecodeFrozenBitmap(bytes)
                : null;
        }
        catch
        {
            return null;
        }
    }

    private static BitmapImage? TryDecodeFrozenBitmap(byte[] bytes)
    {
        try
        {
            using var ms = new MemoryStream(bytes);
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.DecodePixelWidth = 32;
            bitmap.StreamSource = ms;
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    public static bool IsBlockedInternalHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return true;
        }

        var clean = host.Trim().Trim('[', ']');
        if (string.Equals(clean, "localhost", StringComparison.OrdinalIgnoreCase) ||
            clean.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase) ||
            clean.EndsWith(".local", StringComparison.OrdinalIgnoreCase) ||
            clean.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(clean, out var ip) && IsBlockedIpAddress(ip);
    }

    public static bool IsBlockedIpAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any))
        {
            return true;
        }

        if (ip.IsIPv4MappedToIPv6)
        {
            ip = ip.MapToIPv4();
        }

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == AddressFamily.InterNetwork && bytes.Length == 4)
        {
            // Block 0.0.0.0/8, 10.0.0.0/8, 100.64.0.0/10 (CGNAT), 127.0.0.0/8,
            // 169.254.0.0/16 (link-local/IMDS), 172.16.0.0/12, 192.168.0.0/16, >=224.0.0.0 (multicast/reserved)
            return bytes[0] == 0
                || bytes[0] == 10
                || (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127)
                || bytes[0] == 127
                || (bytes[0] == 169 && bytes[1] == 254)
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] >= 224;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && bytes.Length == 16)
        {
            return ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast || (bytes[0] & 0xFE) == 0xFC;
        }

        return false;
    }

    [GeneratedRegex("[^a-z0-9.-]")]
    private static partial Regex SafeFileRegex();

    [GeneratedRegex(@"<link\b[^>]*\brel\s*=\s*[""'][^""']*\bicon\b[^""']*[""'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex IconLinkTagRegex();

    [GeneratedRegex(@"\bhref\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefAttrRegex();
}
