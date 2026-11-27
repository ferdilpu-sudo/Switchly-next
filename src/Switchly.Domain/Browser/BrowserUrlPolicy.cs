namespace Switchly.Domain.Browser;

/// <summary>
/// Enforces allowed navigation URL schemes (http, https, about:blank) and resolves
/// omnibox / address bar input into either a safe URL or a search query.
/// </summary>
public static class BrowserUrlPolicy
{
    public const string BlankUrl = "about:blank";

    public static bool IsAllowedNavigationUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var trimmed = url.Trim();
        if (string.Equals(trimmed, BlankUrl, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Allows OAuth popups that start blank (empty or about:blank) as well as http/https URLs,
    /// while blocking dangerous schemes such as file:, chrome:, and javascript:.
    /// </summary>
    public static bool IsAllowedPopupUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        return IsAllowedNavigationUrl(url);
    }

    /// <summary>
    /// Allows web download schemes (http, https, blob, data) while blocking local filesystem schemes (file:).
    /// </summary>
    public static bool IsAllowedDownloadUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        return string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, "blob", StringComparison.OrdinalIgnoreCase)
            || string.Equals(uri.Scheme, "data", StringComparison.OrdinalIgnoreCase);
    }

    private static readonly HashSet<string> BlockedNonWebSchemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "file", "javascript", "vbscript", "data", "blob",
        "chrome", "chrome-extension", "chrome-untrusted", "edge", "devtools",
        "view-source", "about", "ftp", "ws", "wss", "mailto", "tel", "ms-settings",
    };

    /// <summary>
    /// Resolves user input from the Address Bar:
    /// - Blocks dangerous/internal schemes (file:, chrome:, javascript:, data:, etc.) by returning null.
    /// - Accepts explicit http/https URLs and about:blank.
    /// - Converts bare hostnames (e.g. chatgpt.com, localhost:3000) to https:// (or http:// for localhost).
    /// - Converts plain search phrases (including colons like "python: sort list" or "site:github.com switchly") to Google search.
    /// </summary>
    public static string? ResolveAddressBarInput(string? rawInput)
    {
        if (string.IsNullOrWhiteSpace(rawInput))
        {
            return null;
        }

        var trimmed = rawInput.Trim();
        if (string.Equals(trimmed, BlankUrl, StringComparison.OrdinalIgnoreCase))
        {
            return BlankUrl;
        }

        var hasWhitespace = trimmed.Any(char.IsWhiteSpace);

        // Explicit scheme:// URLs: allow only http:// and https://; block all other scheme:// inputs.
        var schemeSepIdx = trimmed.IndexOf("://", StringComparison.Ordinal);
        if (schemeSepIdx > 0 && !hasWhitespace)
        {
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var explicitUri)
                && (string.Equals(explicitUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(explicitUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return explicitUri.AbsoluteUri;
            }

            return null;
        }

        // Colon schemes without "://" (e.g. javascript:alert(1), file:..., data:..., about:config):
        // block known dangerous/browser schemes, while allowing search queries like "python: sort list" or "site:reddit.com switchly".
        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex > 0)
        {
            var beforeColon = trimmed[..colonIndex];
            if (BlockedNonWebSchemes.Contains(beforeColon))
            {
                return null;
            }
        }

        var isLocalhost = trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("localhost/", StringComparison.OrdinalIgnoreCase);

        if (!hasWhitespace && (isLocalhost || (trimmed.Contains('.') && colonIndex < 0) || (colonIndex > 0 && trimmed[..colonIndex].Contains('.'))))
        {
            var scheme = isLocalhost ? "http://" : "https://";
            var candidate = scheme + trimmed;
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var hostUri)
                && !string.IsNullOrWhiteSpace(hostUri.Host))
            {
                return hostUri.AbsoluteUri;
            }
        }

        return $"https://www.google.com/search?q={Uri.EscapeDataString(trimmed)}";
    }
}
