using Microsoft.Data.Sqlite;
using Switchly.Application.Import;
using Switchly.Domain.History;
using Switchly.Domain.Import;

namespace Switchly.Infrastructure.Import;

/// <summary>
/// Read-only importer for Chromium SQLite History databases (TASK-025).
/// Detects locked databases cleanly without killing source browser processes (TEST-018),
/// handles schema-version differences, and rejects malformed databases safely.
/// </summary>
public sealed class ChromiumHistoryImporter : IHistoryImporter
{
    // Chromium stores timestamps as microseconds since 1601-01-01T00:00:00Z.
    private static readonly DateTimeOffset WebKitEpoch = new(1601, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public HistoryParseResult ParseChromiumHistoryFile(string historyDatabasePath)
    {
        var warnings = new List<ImportWarning>();
        if (string.IsNullOrWhiteSpace(historyDatabasePath) || !File.Exists(historyDatabasePath))
        {
            warnings.Add(new ImportWarning("History", "HistoryNotFound", "File database History tidak ditemukan di profil sumber."));
            return new HistoryParseResult(Array.Empty<ImportedHistoryItem>(), 0, false, warnings);
        }

        try
        {
            var info = new FileInfo(historyDatabasePath);
            if (info.Length > ImportLimits.MaxSourceFileBytes)
            {
                warnings.Add(new ImportWarning("History", "HistoryFileTooLarge", "Ukuran file History melebihi batas aman impor."));
                return new HistoryParseResult(Array.Empty<ImportedHistoryItem>(), 0, false, warnings);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add(new ImportWarning("History", "HistoryReadError", "Tidak dapat memeriksa file database History."));
            return new HistoryParseResult(Array.Empty<ImportedHistoryItem>(), 0, false, warnings);
        }

        var tempCopyPath = Path.Combine(Path.GetTempPath(), $"switchly-history-import-{Guid.NewGuid():N}.db");
        try
        {
            try
            {
                CopyReadOnlySnapshot(historyDatabasePath, tempCopyPath);
            }
            catch (IOException)
            {
                warnings.Add(new ImportWarning(
                    "History",
                    "HistoryDatabaseLocked",
                    "Database riwayat sedang terkunci oleh browser sumber. Tutup Chrome/Edge terlebih dahulu lalu coba lagi."));
                return new HistoryParseResult(Array.Empty<ImportedHistoryItem>(), 0, true, warnings);
            }
            catch (UnauthorizedAccessException)
            {
                warnings.Add(new ImportWarning(
                    "History",
                    "HistoryAccessDenied",
                    "Akses ke database riwayat ditolak. Pastikan browser sumber sudah ditutup."));
                return new HistoryParseResult(Array.Empty<ImportedHistoryItem>(), 0, true, warnings);
            }

            return ReadHistoryFromSnapshot(tempCopyPath, warnings);
        }
        finally
        {
            TryDeleteFile(tempCopyPath);
        }
    }

    private static void CopyReadOnlySnapshot(string sourcePath, string destinationPath)
    {
        using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var dest = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        source.CopyTo(dest);
    }

    private static HistoryParseResult ReadHistoryFromSnapshot(string snapshotPath, List<ImportWarning> warnings)
    {
        var entries = new List<ImportedHistoryItem>();
        var skipped = 0;
        var unsafeUrlCount = 0;

        try
        {
            var connString = new SqliteConnectionStringBuilder
            {
                DataSource = snapshotPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false,
            }.ToString();

            using var connection = new SqliteConnection(connString);
            connection.Open();

            // Verify urls table exists and inspect available columns for schema-version tolerance.
            var columns = ReadTableColumns(connection, "urls");
            if (columns.Count == 0 || !columns.Contains("url"))
            {
                warnings.Add(new ImportWarning(
                    "History",
                    "HistorySchemaUnsupported",
                    "Skema database History tidak dikenali atau versi tidak didukung (tabel/kolom 'urls.url' tidak ditemukan)."));
                return new HistoryParseResult(Array.Empty<ImportedHistoryItem>(), 0, false, warnings);
            }

            var hasTitle = columns.Contains("title");
            var hasVisitCount = columns.Contains("visit_count");
            var hasLastVisitTime = columns.Contains("last_visit_time");

            var selectCols = new List<string> { "url" };
            selectCols.Add(hasTitle ? "title" : "NULL AS title");
            selectCols.Add(hasVisitCount ? "visit_count" : "1 AS visit_count");
            selectCols.Add(hasLastVisitTime ? "last_visit_time" : "0 AS last_visit_time");

            var orderBy = hasLastVisitTime ? "ORDER BY last_visit_time DESC" : string.Empty;
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"SELECT {string.Join(", ", selectCols)} FROM urls {orderBy} LIMIT {ImportLimits.MaxHistoryEntries + 1};";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (entries.Count >= ImportLimits.MaxHistoryEntries)
                {
                    warnings.Add(new ImportWarning(
                        "History",
                        "HistoryLimitReached",
                        $"Batas maksimum {ImportLimits.MaxHistoryEntries} entri riwayat tercapai."));
                    break;
                }

                var rawUrl = reader.IsDBNull(0) ? null : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(rawUrl))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    // Strip query strings/credentials/fragments for history metadata hygiene (schema.md §3 & §5).
                    var sanitizedUrl = UrlSanitizer.SanitizeHttpUrl(rawUrl, retainQueryString: false);
                    var rawTitle = reader.IsDBNull(1) ? null : reader.GetString(1);
                    var title = string.IsNullOrWhiteSpace(rawTitle) ? null : UrlSanitizer.NormalizeTitle(rawTitle, sanitizedUrl);
                    var visitCount = reader.IsDBNull(2) ? 1 : Math.Max(1, reader.GetInt32(2));
                    var webkitMicros = reader.IsDBNull(3) ? 0L : reader.GetInt64(3);
                    var visitedAt = ConvertWebKitTimestamp(webkitMicros);

                    entries.Add(new ImportedHistoryItem(sanitizedUrl, title, visitCount, visitedAt));
                }
                catch (ArgumentException)
                {
                    skipped++;
                    unsafeUrlCount++;
                }
            }

            if (unsafeUrlCount > 0)
            {
                warnings.Add(new ImportWarning(
                    "History",
                    "HistoryUnsafeUrlsSkipped",
                    $"{unsafeUrlCount} entri riwayat dengan skema URL non-HTTP/tidak aman dilewati."));
            }

            return new HistoryParseResult(entries, skipped, false, warnings);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode is 5 or 6) // SQLITE_BUSY / SQLITE_LOCKED
        {
            warnings.Add(new ImportWarning(
                "History",
                "HistoryDatabaseLocked",
                "Database riwayat sedang terkunci oleh browser sumber. Tutup Chrome/Edge terlebih dahulu lalu coba lagi."));
            return new HistoryParseResult(Array.Empty<ImportedHistoryItem>(), skipped, true, warnings);
        }
        catch (SqliteException)
        {
            warnings.Add(new ImportWarning(
                "History",
                "HistoryMalformedError",
                "File database History rusak atau bukan database SQLite yang valid."));
            return new HistoryParseResult(Array.Empty<ImportedHistoryItem>(), skipped, false, warnings);
        }
    }

    private static HashSet<string> ReadTableColumns(SqliteConnection connection, string tableName)
    {
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA table_info({tableName});";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            if (!reader.IsDBNull(1))
            {
                columns.Add(reader.GetString(1));
            }
        }

        return columns;
    }

    private static DateTimeOffset ConvertWebKitTimestamp(long microsecondsSince1601)
    {
        if (microsecondsSince1601 <= 0)
        {
            return DateTimeOffset.UtcNow;
        }

        try
        {
            // Convert microseconds to .NET ticks (10 ticks per microsecond).
            var ticks = checked(microsecondsSince1601 * 10L);
            var result = WebKitEpoch.AddTicks(ticks);
            if (result.Year is < 1990 or > 2100)
            {
                return DateTimeOffset.UtcNow;
            }

            return result;
        }
        catch (OverflowException)
        {
            return DateTimeOffset.UtcNow;
        }
        catch (ArgumentOutOfRangeException)
        {
            return DateTimeOffset.UtcNow;
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort temp cleanup.
        }
    }
}
