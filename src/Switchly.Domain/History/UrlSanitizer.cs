namespace Switchly.Domain.History;

/// <summary>
/// Validates and sanitizes http/https URLs for bookmarks and history (schema.md §3 & §5):
/// rejects unsafe schemes (javascript:, file:, data:), strips embedded userinfo credentials,
/// strips fragments, and strips query strings by default for history privacy.
/// </summary>
public static class UrlSanitizer
{
    public const int MaxTitleLength = 512;
    public const int MaxUrlLength = 2048;

    public static string SanitizeHttpUrl(string? rawUrl, bool retainQueryString)
    {
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            throw new ArgumentException("URL is required.", nameof(rawUrl));
        }

        var trimmed = rawUrl.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("URL must be a valid absolute http/https URI.", nameof(rawUrl));
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only http and https URLs are permitted.", nameof(rawUrl));
        }

        var builder = new UriBuilder(uri)
        {
            UserName = string.Empty,
            Password = string.Empty,
            Fragment = string.Empty,
        };

        if (!retainQueryString)
        {
            builder.Query = string.Empty;
        }

        // Normalize default ports so UriBuilder does not emit :443 / :80.
        if (uri.IsDefaultPort)
        {
            builder.Port = -1;
        }

        var result = builder.Uri.AbsoluteUri;
        if (result.Length > MaxUrlLength)
        {
            throw new ArgumentException($"URL exceeds maximum length of {MaxUrlLength} characters.", nameof(rawUrl));
        }

        return result;
    }

    public static string NormalizeTitle(string? title, string fallback)
    {
        var candidate = string.IsNullOrWhiteSpace(title) ? fallback : title.Trim();
        return candidate.Length > MaxTitleLength ? candidate[..MaxTitleLength] : candidate;
    }
}
