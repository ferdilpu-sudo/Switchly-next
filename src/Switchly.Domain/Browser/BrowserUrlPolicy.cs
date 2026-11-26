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

    /// <summary>
    /// Resolves user input from the Address Bar:
    /// - Blocks dangerous/internal schemes (file:, chrome:, javascript:, data:, etc.) by returning null.
    /// - Accepts explicit http/https URLs and about:blank.
    /// - Converts bare hostnames (e.g. chatgpt.com, localhost:3000) to https:// (or http:// for localhost).
    /// - Converts plain search phrases to a Google search URL.
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

        // Reject explicit dangerous/non-web schemes (including "javascript:..." without "://").
        var colonIndex = trimmed.IndexOf(':');
        if (colonIndex > 0)
        {
            var beforeColon = trimmed[..colonIndex];
            var isSchemePrefix = beforeColon.All(c => char.IsLetterOrDigit(c) || c is '+' or '-' or '.');
            var looksLikeHostPort = trimmed.Contains("://", StringComparison.Ordinal) == false
                && (beforeColon.Contains('.') || string.Equals(beforeColon, "localhost", StringComparison.OrdinalIgnoreCase));

            if (isSchemePrefix && !looksLikeHostPort)
            {
                if (Uri.TryCreate(trimmed, UriKind.Absolute, out var explicitUri)
                    && (string.Equals(explicitUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                        || string.Equals(explicitUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
                {
                    return explicitUri.AbsoluteUri;
                }

                // Blocked scheme (file:, chrome:, javascript:, data:, etc.)
                return null;
            }
        }

        var hasWhitespace = trimmed.Any(char.IsWhiteSpace);
        var isLocalhost = trimmed.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("localhost:", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("localhost/", StringComparison.OrdinalIgnoreCase);

        if (!hasWhitespace && (isLocalhost || trimmed.Contains('.')))
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
