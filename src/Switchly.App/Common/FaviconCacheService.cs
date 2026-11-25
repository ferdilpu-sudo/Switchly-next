using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Switchly.App.Common;

/// <summary>
/// Lightweight local-cached favicon fetcher for Web Workspace Groups (D10).
/// Treats downloaded bytes as untrusted input (size-capped, timeout-bounded, frozen BitmapImage)
/// and gracefully returns null on offline/failure so the UI falls back to the vector Globe icon.
/// </summary>
public static partial class FaviconCacheService
{
    private const int MaxIconBytes = 256 * 1024;
    private const int MaxHtmlProbeBytes = 64 * 1024;

    private static readonly HttpClient Http = CreateHttpClient();

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        return client;
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
            string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        var host = uri.Host.ToLowerInvariant();
        if (MemoryCache.TryGetValue(host, out var cached) && cached is not null)
        {
            return cached;
        }

        // Normalize flow.google to its canonical domain flow.google.com so the official Google Flow favicon resolves cleanly.
        var effectiveUrl = string.Equals(host, "flow.google", StringComparison.OrdinalIgnoreCase)
            ? "https://flow.google.com"
            : url;
        var effectiveHost = Uri.TryCreate(effectiveUrl, UriKind.Absolute, out var effUri) && !string.IsNullOrWhiteSpace(effUri.Host)
            ? effUri.Host.ToLowerInvariant()
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
            // Non-critical cache write failure; keep decoded in-memory image.
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

    private static async Task<string?> TryExtractHtmlIconUrlAsync(string pageUrl)
    {
        try
        {
            using var response = await Http.GetAsync(pageUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
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

                var rawHref = System.Net.WebUtility.HtmlDecode(hrefMatch.Groups[1].Value.Trim());
                if (string.IsNullOrWhiteSpace(rawHref) || rawHref.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Uri.TryCreate(baseUri, rawHref, out var resolved) &&
                    (resolved.Scheme == Uri.UriSchemeHttp || resolved.Scheme == Uri.UriSchemeHttps))
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
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            return response.RequestMessage?.RequestUri?.Host?.ToLowerInvariant();
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
            using var response = await Http.GetAsync(iconUrl, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode)
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

    [GeneratedRegex("[^a-z0-9.-]")]
    private static partial Regex SafeFileRegex();

    [GeneratedRegex(@"<link\b[^>]*\brel\s*=\s*[""'][^""']*\bicon\b[^""']*[""'][^>]*>", RegexOptions.IgnoreCase)]
    private static partial Regex IconLinkTagRegex();

    [GeneratedRegex(@"\bhref\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase)]
    private static partial Regex HrefAttrRegex();
}
