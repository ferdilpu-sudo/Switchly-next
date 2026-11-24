using System.Text.RegularExpressions;

namespace Switchly.Domain.Profiles;

/// <summary>Pure domain validation and normalization rules for BrowserProfile (ENT-001, schema.md §5).</summary>
public static partial class ProfileValidation
{
    public const int MaxNameLength = 80;
    public const int MaxIconKeyLength = 64;
    public const int MaxStartupUrlLength = 2048;

    public static string NormalizeAndValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Profile name is required.", nameof(name));
        }

        var trimmed = name.Trim();
        if (trimmed.Length > MaxNameLength)
        {
            throw new ArgumentException($"Profile name must be at most {MaxNameLength} characters.", nameof(name));
        }

        return trimmed;
    }

    public static string? NormalizeAndValidateColorHex(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return null;
        }

        var trimmed = colorHex.Trim();
        if (!HexColorRegex().IsMatch(trimmed))
        {
            throw new ArgumentException("ColorHex must match #RRGGBB.", nameof(colorHex));
        }

        return trimmed.ToUpperInvariant();
    }

    public static string? NormalizeAndValidateIconKey(string? iconKey)
    {
        if (string.IsNullOrWhiteSpace(iconKey))
        {
            return null;
        }

        var trimmed = iconKey.Trim();
        if (trimmed.Length > MaxIconKeyLength || !IconKeyRegex().IsMatch(trimmed))
        {
            throw new ArgumentException("IconKey contains invalid characters or exceeds length limit.", nameof(iconKey));
        }

        return trimmed;
    }

    public static string? NormalizeAndValidateStartupUrl(string? startupUrl)
    {
        if (string.IsNullOrWhiteSpace(startupUrl))
        {
            return null;
        }

        var trimmed = startupUrl.Trim();
        if (trimmed.Length > MaxStartupUrlLength)
        {
            throw new ArgumentException($"StartupUrl must be at most {MaxStartupUrlLength} characters.", nameof(startupUrl));
        }

        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("StartupUrl must be a valid absolute http/https URL.", nameof(startupUrl));
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Only http and https schemes are allowed for StartupUrl.", nameof(startupUrl));
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new ArgumentException("StartupUrl must not contain embedded credentials.", nameof(startupUrl));
        }

        return uri.AbsoluteUri;
    }

    /// <summary>Generates a single-segment directory name directly under RootCachePath (ADR-011).</summary>
    public static string GenerateStorageRelativePath(Guid profileId)
    {
        if (profileId == Guid.Empty)
        {
            throw new ArgumentException("ProfileId must not be empty.", nameof(profileId));
        }

        return profileId.ToString("N");
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    [GeneratedRegex("^[a-zA-Z0-9._-]+$")]
    private static partial Regex IconKeyRegex();
}
