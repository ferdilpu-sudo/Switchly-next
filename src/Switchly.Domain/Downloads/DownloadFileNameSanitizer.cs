using System.Text;

namespace Switchly.Domain.Downloads;

/// <summary>
/// Sanitizes untrusted download filenames for Windows (schema.md §3 ENT-007):
/// strips path separators, NTFS ADS colons, invalid chars, Unicode bidi/RLO spoofing chars,
/// trailing dots/spaces, and neutralizes Windows reserved device names (CON, NUL, COM1..LPT9).
/// </summary>
public static class DownloadFileNameSanitizer
{
    public const int MaxLength = 255;
    private const string DefaultFallback = "download.bin";

    private static readonly HashSet<string> ReservedDeviceNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    private static readonly HashSet<char> InvalidChars =
    [
        '<', '>', ':', '"', '/', '\\', '|', '?', '*',
    ];

    public static string Sanitize(string? suggestedFileName)
    {
        if (string.IsNullOrWhiteSpace(suggestedFileName))
        {
            return DefaultFallback;
        }

        // Extract only the final segment if an attacker supplied path separators.
        var lastSlash = suggestedFileName.LastIndexOfAny(['/', '\\']);
        var leaf = lastSlash >= 0 ? suggestedFileName[(lastSlash + 1)..] : suggestedFileName;

        var sb = new StringBuilder(leaf.Length);
        foreach (var ch in leaf)
        {
            if (char.IsControl(ch) || IsBidiControl(ch))
            {
                continue;
            }

            if (InvalidChars.Contains(ch))
            {
                sb.Append('_');
                continue;
            }

            sb.Append(ch);
        }

        var cleaned = sb.ToString().Trim().TrimEnd('.', ' ');
        if (cleaned.Length > MaxLength)
        {
            var ext = Path.GetExtension(cleaned);
            if (ext.Length >= MaxLength)
            {
                cleaned = cleaned[..MaxLength];
            }
            else
            {
                cleaned = cleaned[..(MaxLength - ext.Length)].TrimEnd('.', ' ') + ext;
            }
        }

        cleaned = cleaned.TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned is "." or "..")
        {
            return DefaultFallback;
        }

        var firstDot = cleaned.IndexOf('.');
        var stem = (firstDot >= 0 ? cleaned[..firstDot] : cleaned).Trim();
        if (ReservedDeviceNames.Contains(stem))
        {
            cleaned = "_" + cleaned;
            if (cleaned.Length > MaxLength)
            {
                cleaned = cleaned[..MaxLength].TrimEnd('.', ' ');
            }
        }

        return string.IsNullOrWhiteSpace(cleaned) ? DefaultFallback : cleaned;
    }

    private static bool IsBidiControl(char ch)
        => (ch >= '\u202A' && ch <= '\u202E') || (ch >= '\u2066' && ch <= '\u2069') || ch == '\u200E' || ch == '\u200F';
}
