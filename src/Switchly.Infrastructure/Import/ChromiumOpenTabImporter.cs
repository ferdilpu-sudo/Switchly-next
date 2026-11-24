using System.Buffers.Binary;
using System.Text;
using Switchly.Application.Import;
using Switchly.Domain.History;
using Switchly.Domain.Import;

namespace Switchly.Infrastructure.Import;

/// <summary>
/// Read-only bounded parser for Chromium SNSS session/tab files (TASK-026).
/// Extracts safe http/https URLs only; explicitly reports unsupported/unavailable formats
/// instead of silently skipping them.
/// </summary>
public sealed class ChromiumOpenTabImporter : IOpenTabImporter
{
    private const uint SnssMagic = 0x53534E53; // "SNSS" in little-endian
    private const byte CommandUpdateTabNavigationV1 = 1;
    private const byte CommandUpdateTabNavigationSession = 6;

    public OpenTabsParseResult ParseChromiumSessionsDirectory(string profileDirectoryPath)
    {
        var warnings = new List<ImportWarning>();
        if (string.IsNullOrWhiteSpace(profileDirectoryPath))
        {
            warnings.Add(new ImportWarning("OpenTabs", "OpenTabsNotFound", "Direktori profil sumber tidak valid."));
            return new OpenTabsParseResult(Array.Empty<ImportedTabItem>(), 0, warnings);
        }

        var sessionsDir = Path.Combine(profileDirectoryPath, "Sessions");
        if (!Directory.Exists(sessionsDir))
        {
            warnings.Add(new ImportWarning("OpenTabs", "OpenTabsNotFound", "Folder Sessions tidak ditemukan di profil sumber."));
            return new OpenTabsParseResult(Array.Empty<ImportedTabItem>(), 0, warnings);
        }

        var candidateFiles = Directory.EnumerateFiles(sessionsDir)
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return name.StartsWith("Tabs_", StringComparison.OrdinalIgnoreCase) ||
                       name.StartsWith("Session_", StringComparison.OrdinalIgnoreCase);
            })
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(4)
            .ToList();

        if (candidateFiles.Count == 0)
        {
            warnings.Add(new ImportWarning("OpenTabs", "OpenTabsNotFound", "Tidak ada file sesi tab (SNSS) yang ditemukan."));
            return new OpenTabsParseResult(Array.Empty<ImportedTabItem>(), 0, warnings);
        }

        var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var tabs = new List<ImportedTabItem>();
        var skipped = 0;
        var parsedAnyValidSnss = false;

        foreach (var file in candidateFiles)
        {
            if (tabs.Count >= ImportLimits.MaxOpenTabs)
            {
                break;
            }

            try
            {
                var info = new FileInfo(file);
                if (info.Length < 8 || info.Length > ImportLimits.MaxSourceFileBytes)
                {
                    continue;
                }

                byte[] bytes;
                using (var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    bytes = new byte[info.Length];
                    stream.ReadExactly(bytes);
                }

                var magic = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(0, 4));
                var version = BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(4, 4));
                if (magic != SnssMagic || (version != 1 && version != 3))
                {
                    warnings.Add(new ImportWarning(
                        "OpenTabs",
                        "OpenTabsUnsupportedFormat",
                        $"File sesi '{Path.GetFileName(file)}' menggunakan format atau versi SNSS yang tidak didukung (versi {version})."));
                    continue;
                }

                parsedAnyValidSnss = true;
                ParseSnssCommands(bytes.AsSpan(8), tabs, seenUrls, ref skipped);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                warnings.Add(new ImportWarning(
                    "OpenTabs",
                    "OpenTabsReadError",
                    $"File sesi '{Path.GetFileName(file)}' tidak dapat dibaca (kemungkinan sedang digunakan browser sumber)."));
            }
        }

        if (!parsedAnyValidSnss && warnings.Count == 0)
        {
            warnings.Add(new ImportWarning(
                "OpenTabs",
                "OpenTabsUnsupportedFormat",
                "Format file sesi tab terbuka tidak dapat diurai."));
        }

        return new OpenTabsParseResult(tabs, skipped, warnings);
    }

    private static void ParseSnssCommands(
        ReadOnlySpan<byte> data,
        List<ImportedTabItem> tabs,
        HashSet<string> seenUrls,
        ref int skipped)
    {
        var offset = 0;
        while (offset + 2 <= data.Length && tabs.Count < ImportLimits.MaxOpenTabs)
        {
            var payloadSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2));
            offset += 2;

            if (payloadSize == 0 || offset + payloadSize > data.Length)
            {
                break;
            }

            var commandId = data[offset];
            var payload = data.Slice(offset + 1, payloadSize - 1);
            offset += payloadSize;

            if (commandId is CommandUpdateTabNavigationV1 or CommandUpdateTabNavigationSession)
            {
                if (TryExtractNavigationFromPickle(payload, out var rawUrl, out var rawTitle))
                {
                    try
                    {
                        var sanitizedUrl = UrlSanitizer.SanitizeHttpUrl(rawUrl, retainQueryString: false);
                        if (seenUrls.Add(sanitizedUrl))
                        {
                            var title = string.IsNullOrWhiteSpace(rawTitle)
                                ? null
                                : UrlSanitizer.NormalizeTitle(rawTitle, sanitizedUrl);
                            tabs.Add(new ImportedTabItem(sanitizedUrl, title));
                        }
                    }
                    catch (ArgumentException)
                    {
                        skipped++;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Parses Chromium SerializedNavigationEntry Pickle layout:
    /// [uint32 pickle_payload_size][int32 tab_id][int32 nav_index][uint32 url_len][utf8 url + 4-byte align][uint32 title_char_len][utf16le title + 4-byte align].
    /// </summary>
    private static bool TryExtractNavigationFromPickle(ReadOnlySpan<byte> payload, out string url, out string? title)
    {
        url = string.Empty;
        title = null;

        // Minimum: 4 (pickle size) + 4 (tab_id) + 4 (nav_index) + 4 (url_len) = 16 bytes
        if (payload.Length < 16)
        {
            return false;
        }

        var pos = 12; // skip pickle_payload_size, tab_id, nav_index
        var urlByteLen = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(pos, 4));
        pos += 4;

        if (urlByteLen <= 0 || urlByteLen > 2048 || pos + urlByteLen > payload.Length)
        {
            return false;
        }

        url = Encoding.UTF8.GetString(payload.Slice(pos, urlByteLen));
        pos += Align4(urlByteLen);

        if (pos + 4 <= payload.Length)
        {
            var titleCharLen = BinaryPrimitives.ReadInt32LittleEndian(payload.Slice(pos, 4));
            pos += 4;
            var titleByteLen = titleCharLen * 2;
            if (titleCharLen > 0 && titleCharLen <= 512 && pos + titleByteLen <= payload.Length)
            {
                title = Encoding.Unicode.GetString(payload.Slice(pos, titleByteLen));
            }
        }

        return true;
    }

    private static int Align4(int length) => (length + 3) & ~3;
}
