using System.Globalization;

namespace Switchly.Domain.Groups;

/// <summary>Pure domain validation and IDNA domain normalization for ProfileGroup (ENT-002, schema.md §5).</summary>
public static class GroupValidation
{
    public const int MaxNameLength = 80;
    public const int MaxDomainKeyLength = 253;

    public static string NormalizeAndValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Group name is required.", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException($"Group name must be at most {MaxNameLength} characters.", nameof(name));
        }

        return trimmed;
    }

    /// <summary>
    /// Extracts and normalizes a domain key from an absolute http/https URL (or bare hostname)
    /// using IDNA ASCII, lowercase, and stripping a leading "www." prefix.
    /// </summary>
    public static string NormalizeDomainKey(string? urlOrHost)
    {
        if (string.IsNullOrWhiteSpace(urlOrHost))
        {
            throw new ArgumentException("URL or domain is required.", nameof(urlOrHost));
        }

        var trimmed = urlOrHost.Trim();
        string host;

        if (trimmed.Contains("://", StringComparison.Ordinal))
        {
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException("URL is not a valid absolute URI.", nameof(urlOrHost));
            }

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("Only http and https URLs can form WebsiteDomain groups.", nameof(urlOrHost));
            }

            host = uri.IdnHost;
        }
        else
        {
            if (trimmed.Contains(':', StringComparison.Ordinal))
            {
                throw new ArgumentException("Unsupported URI scheme or port in domain input.", nameof(urlOrHost));
            }

            if (!Uri.TryCreate("https://" + trimmed, UriKind.Absolute, out var syntheticUri))
            {
                throw new ArgumentException("Domain host is invalid.", nameof(urlOrHost));
            }

            host = syntheticUri.IdnHost;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            throw new ArgumentException("Domain host could not be extracted.", nameof(urlOrHost));
        }

        var idn = new IdnMapping();
        var asciiHost = idn.GetAscii(host).TrimEnd('.').ToLowerInvariant();
        if (asciiHost.StartsWith("www.", StringComparison.Ordinal) && asciiHost.Length > 4)
        {
            asciiHost = asciiHost[4..];
        }

        if (asciiHost.Length == 0 || asciiHost.Length > MaxDomainKeyLength)
        {
            throw new ArgumentException("Normalized domain key is empty or exceeds max length.", nameof(urlOrHost));
        }

        return asciiHost;
    }
}
