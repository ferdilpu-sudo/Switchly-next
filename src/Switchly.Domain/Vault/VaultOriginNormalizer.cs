using System.Globalization;

namespace Switchly.Domain.Vault;

/// <summary>
/// Normalizes website URLs to a canonical http/https origin (scheme://host[:port]) without path, query, fragment, or userinfo (ENT-009, schema.md §5).
/// </summary>
public static class VaultOriginNormalizer
{
    private static readonly IdnMapping Idn = new();

    public static string NormalizeOrigin(string? rawUrlOrOrigin)
    {
        if (string.IsNullOrWhiteSpace(rawUrlOrOrigin))
        {
            throw new ArgumentException("Origin URL is required.", nameof(rawUrlOrOrigin));
        }

        var trimmed = rawUrlOrOrigin.Trim();
        if (!trimmed.Contains("://", StringComparison.Ordinal))
        {
            trimmed = "https://" + trimmed;
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Invalid URL format for credential origin.", nameof(rawUrlOrOrigin));
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        if (scheme is not ("https" or "http"))
        {
            throw new ArgumentException("Only https and http origins are allowed for credentials.", nameof(rawUrlOrOrigin));
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("Credential origin must have a valid host.", nameof(rawUrlOrOrigin));
        }

        string asciiHost;
        try
        {
            asciiHost = Idn.GetAscii(uri.Host).ToLowerInvariant();
        }
        catch (ArgumentException)
        {
            throw new ArgumentException("Credential origin host is not a valid domain.", nameof(rawUrlOrOrigin));
        }

        return uri.IsDefaultPort
            ? $"{scheme}://{asciiHost}"
            : $"{scheme}://{asciiHost}:{uri.Port}";
    }

    public static bool TryNormalizeOrigin(string? rawUrlOrOrigin, out string origin)
    {
        try
        {
            origin = NormalizeOrigin(rawUrlOrOrigin);
            return true;
        }
        catch (ArgumentException)
        {
            origin = string.Empty;
            return false;
        }
    }
}
