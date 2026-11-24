using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Switchly.Application.Profiles;
using Switchly.Application.Security;
using Switchly.Domain.Profiles;
using Switchly.Domain.Security;

namespace Switchly.Infrastructure.Security;

/// <summary>
/// Manual portable backup & staged restore service (TASK-031, TASK-033, ADR-006, D2, D3).
/// Excludes CEF global state, regenerable caches, and device-bound Chromium cookies.
/// </summary>
public sealed class PortableBackupRestoreService : IBackupRestoreService
{
    private readonly string _appDataRoot;
    private readonly string _dataDirectory;
    private readonly string _profilesRootPath;
    private readonly string _databasePath;
    private readonly IProfileRepository _profileRepository;
    private readonly IBackupJobRepository _backupJobRepository;
    private readonly TimeProvider _timeProvider;
    private readonly int? _kdfIterationsOverride;

    public PortableBackupRestoreService(
        string appDataRoot,
        IProfileRepository profileRepository,
        IBackupJobRepository backupJobRepository,
        TimeProvider? timeProvider = null,
        int? kdfIterationsOverride = null)
    {
        if (string.IsNullOrWhiteSpace(appDataRoot))
        {
            throw new ArgumentException("AppData root is required.", nameof(appDataRoot));
        }

        _appDataRoot = Path.GetFullPath(appDataRoot);
        _dataDirectory = Path.Combine(_appDataRoot, "Data");
        _profilesRootPath = Path.Combine(_appDataRoot, "Profiles");
        _databasePath = Path.Combine(_dataDirectory, "switchly-next.db");
        _profileRepository = profileRepository ?? throw new ArgumentNullException(nameof(profileRepository));
        _backupJobRepository = backupJobRepository ?? throw new ArgumentNullException(nameof(backupJobRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _kdfIterationsOverride = kdfIterationsOverride;
    }

    public async Task<BackupCreationResult> CreateBackupAsync(
        string destinationArchivePath,
        string passphrase,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(destinationArchivePath))
        {
            throw new ArgumentException("Lokasi file backup wajib diisi.", nameof(destinationArchivePath));
        }

        if (string.IsNullOrEmpty(passphrase) || passphrase.Length < BackupLimits.MinPassphraseLength)
        {
            throw new ArgumentException($"Passphrase minimal {BackupLimits.MinPassphraseLength} karakter.", nameof(passphrase));
        }

        var fullDestPath = Path.GetFullPath(destinationArchivePath);
        var destDir = Path.GetDirectoryName(fullDestPath);
        if (!string.IsNullOrEmpty(destDir))
        {
            Directory.CreateDirectory(destDir);
        }

        var profiles = await _profileRepository.GetAllAsync(includeArchived: true, cancellationToken);
        var profileIds = profiles.Select(p => p.ProfileId).ToList();

        var job = new BackupJob
        {
            BackupJobId = Guid.NewGuid(),
            Mode = BackupMode.Manual,
            Status = BackupJobStatus.Running,
            DestinationPath = Path.GetFileName(fullDestPath), // Redact full personal directory path in DB/logs (TEST-022)
            StartedAtUtc = _timeProvider.GetUtcNow(),
            IncludedProfilesJson = JsonSerializer.Serialize(profileIds),
            ArchiveVersion = BackupLimits.CurrentArchiveVersion,
            ResultSummaryJson = "{}",
        };
        await _backupJobRepository.AddAsync(job, cancellationToken);

        var tempZipPath = Path.Combine(Path.GetTempPath(), $"switchly-bak-plain-{Guid.NewGuid():N}.zip");
        var tempDbCopyPath = Path.Combine(Path.GetTempPath(), $"switchly-bak-db-{Guid.NewGuid():N}.db");
        var tempArchivePath = fullDestPath + ".tmp";

        try
        {
            progress?.Report(new BackupProgress("Menyalin metadata aplikasi...", 0.1));
            cancellationToken.ThrowIfCancellationRequested();

            // Use SQLite online backup API so WAL/journal state is captured consistently.
            CopySqliteDatabaseSafely(_databasePath, tempDbCopyPath);

            progress?.Report(new BackupProgress("Mengemas profil terisolasi (tanpa cache & cookies perangkat)...", 0.25));
            long estimatedPlainBytes = 0;

            using (var zipStream = new FileStream(tempZipPath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var zip = new ZipArchive(zipStream, ZipArchiveMode.Create, leaveOpen: false))
            {
                AddFileToZip(zip, tempDbCopyPath, "data/switchly-next.db");
                estimatedPlainBytes += new FileInfo(tempDbCopyPath).Length;

                foreach (var profile in profiles)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var rel = profile.StorageRelativePath?.Trim() ?? string.Empty;
                    if (!Guid.TryParseExact(rel, "D", out _))
                    {
                        continue;
                    }
                    var profileDir = Path.Combine(_profilesRootPath, rel);
                    if (!Directory.Exists(profileDir))
                    {
                        continue;
                    }

                    estimatedPlainBytes += AddProfileDirectoryToZip(zip, profileDir, $"profiles/{rel}", cancellationToken);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();

            var manifest = new BackupManifest(
                ArchiveVersion: BackupLimits.CurrentArchiveVersion,
                CreatedAtUtc: _timeProvider.GetUtcNow(),
                ProfileCount: profileIds.Count,
                IncludedProfileIds: profileIds,
                EstimatedPlaintextBytes: estimatedPlainBytes,
                ExcludesDeviceBoundCookies: true);

            using (var plainInput = new FileStream(tempZipPath, FileMode.Open, FileAccess.Read, FileShare.None))
            using (var encOutput = new FileStream(tempArchivePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                ChunkedAesGcmArchiveCrypto.EncryptStream(
                    plainInput,
                    encOutput,
                    manifest,
                    passphrase,
                    _kdfIterationsOverride,
                    progress,
                    cancellationToken);
                encOutput.Flush(flushToDisk: true);
            }

            File.Move(tempArchivePath, fullDestPath, overwrite: true);
            var archiveSize = new FileInfo(fullDestPath).Length;

            job.Status = BackupJobStatus.Completed;
            job.CompletedAtUtc = _timeProvider.GetUtcNow();
            job.ResultSummaryJson = JsonSerializer.Serialize(new
            {
                status = "Completed",
                profileCount = profileIds.Count,
                archiveSizeBytes = archiveSize,
                excludesDeviceBoundCookies = true,
            });
            await _backupJobRepository.UpdateAsync(job, CancellationToken.None);

            progress?.Report(new BackupProgress("Backup selesai.", 1.0));
            return new BackupCreationResult(
                job.BackupJobId,
                fullDestPath,
                profileIds.Count,
                archiveSize,
                job.CompletedAtUtc.Value);
        }
        catch (OperationCanceledException)
        {
            job.Status = BackupJobStatus.Cancelled;
            job.CompletedAtUtc = _timeProvider.GetUtcNow();
            job.ResultSummaryJson = JsonSerializer.Serialize(new { status = "Cancelled" });
            await _backupJobRepository.UpdateAsync(job, CancellationToken.None);
            TryDeleteFile(tempArchivePath);
            throw;
        }
        catch (Exception ex)
        {
            job.Status = BackupJobStatus.Failed;
            job.CompletedAtUtc = _timeProvider.GetUtcNow();
            job.ResultSummaryJson = JsonSerializer.Serialize(new { status = "Failed", errorType = ex.GetType().Name });
            await _backupJobRepository.UpdateAsync(job, CancellationToken.None);
            TryDeleteFile(tempArchivePath);
            throw;
        }
        finally
        {
            TryDeleteFile(tempZipPath);
            TryDeleteFile(tempDbCopyPath);
        }
    }

    public BackupManifest InspectBackup(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            throw new FileNotFoundException("File arsip backup tidak ditemukan.");
        }

        using var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return ChunkedAesGcmArchiveCrypto.InspectHeader(stream);
    }

    public async Task<RestoreStageResult> StageRestoreAsync(
        string archivePath,
        string passphrase,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            throw new FileNotFoundException("File arsip backup tidak ditemukan.");
        }

        var stagingRoot = Path.Combine(_appDataRoot, "RestoreStaging");
        Directory.CreateDirectory(stagingRoot);
        var stagingDir = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stagingDir);

        var tempDecryptedZip = Path.Combine(Path.GetTempPath(), $"switchly-restore-plain-{Guid.NewGuid():N}.zip");
        var succeeded = false;

        try
        {
            BackupManifest manifest;
            using (var archiveStream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var decryptedStream = new FileStream(tempDecryptedZip, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                manifest = ChunkedAesGcmArchiveCrypto.DecryptStream(
                    archiveStream,
                    decryptedStream,
                    passphrase,
                    progress,
                    cancellationToken);
                decryptedStream.Flush();
            }

            progress?.Report(new BackupProgress("Mengekstrak ke folder staging aman...", 0.85));
            var extractedCount = ExtractZipSafelyToStaging(tempDecryptedZip, stagingDir, cancellationToken);

            var stagedDb = Path.Combine(stagingDir, "data", "switchly-next.db");
            if (!File.Exists(stagedDb))
            {
                throw new CryptographicException("Arsip backup tidak memuat database aplikasi (data/switchly-next.db).");
            }

            VerifySqliteIntegrity(stagedDb);

            // Write pending-restore marker atomically so PendingRestoreApplier swaps before Cef.Initialize().
            Directory.CreateDirectory(_dataDirectory);
            var markerPath = PendingRestoreApplier.GetMarkerPath(_appDataRoot);
            var marker = new PendingRestoreMarker(
                StagingDirectoryName: Path.GetFileName(stagingDir),
                ArchiveVersion: manifest.ArchiveVersion,
                CreatedAtUtc: _timeProvider.GetUtcNow());
            var tempMarkerPath = markerPath + ".tmp";
            await File.WriteAllTextAsync(tempMarkerPath, JsonSerializer.Serialize(marker), cancellationToken);
            File.Move(tempMarkerPath, markerPath, overwrite: true);

            succeeded = true;
            progress?.Report(new BackupProgress("Staging pemulihan siap. Restart aplikasi untuk menerapkan.", 1.0));
            return new RestoreStageResult(manifest, stagingDir, extractedCount);
        }
        finally
        {
            TryDeleteFile(tempDecryptedZip);
            if (!succeeded)
            {
                TryDeleteDirectory(stagingDir);
            }
        }
    }

    private static void CopySqliteDatabaseSafely(string sourceDbPath, string destinationDbPath)
    {
        if (!File.Exists(sourceDbPath))
        {
            throw new FileNotFoundException("Database aplikasi tidak ditemukan.");
        }

        using var source = new SqliteConnection($"Data Source={sourceDbPath};Mode=ReadOnly;Pooling=False");
        using var destination = new SqliteConnection($"Data Source={destinationDbPath};Pooling=False");
        source.Open();
        destination.Open();
        source.BackupDatabase(destination);
    }

    private static void VerifySqliteIntegrity(string dbPath)
    {
        using var conn = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly;Pooling=False");
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "PRAGMA quick_check;";
        var result = cmd.ExecuteScalar()?.ToString();
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new CryptographicException("Database di dalam arsip backup gagal verifikasi integritas SQLite.");
        }
    }

    private static long AddProfileDirectoryToZip(
        ZipArchive zip,
        string profileDir,
        string entryPrefix,
        CancellationToken cancellationToken)
    {
        long totalBytes = 0;
        var rootFull = Path.GetFullPath(profileDir);

        foreach (var filePath in Directory.EnumerateFiles(rootFull, "*", SearchOption.AllDirectories))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var relPath = Path.GetRelativePath(rootFull, filePath);
            if (ShouldExcludeRelativePath(relPath))
            {
                continue;
            }

            var zipEntryName = $"{entryPrefix}/{relPath.Replace('\\', '/')}";
            try
            {
                AddFileToZip(zip, filePath, zipEntryName);
                totalBytes += new FileInfo(filePath).Length;
            }
            catch (IOException)
            {
                // Skip transient lock files if any remain after profile close.
            }
        }

        return totalBytes;
    }

    private static bool ShouldExcludeRelativePath(string relativePath)
    {
        var segments = relativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            return true;
        }

        var fileName = segments[^1];
        if (BackupLimits.ExcludedDeviceBoundFileNames.Contains(fileName))
        {
            return true;
        }

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (BackupLimits.ExcludedCacheDirectoryNames.Contains(segments[i]))
            {
                return true;
            }
        }

        return false;
    }

    private static void AddFileToZip(ZipArchive zip, string sourceFilePath, string entryName)
    {
        var entry = zip.CreateEntry(entryName, CompressionLevel.Optimal);
        using var entryStream = entry.Open();
        using var fileStream = new FileStream(sourceFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        fileStream.CopyTo(entryStream);
    }

    private static int ExtractZipSafelyToStaging(string zipPath, string stagingDir, CancellationToken cancellationToken)
    {
        var stagingFull = Path.GetFullPath(stagingDir);
        if (!stagingFull.EndsWith(Path.DirectorySeparatorChar))
        {
            stagingFull += Path.DirectorySeparatorChar;
        }

        var extractedFiles = 0;
        long totalExtractedBytes = 0;

        using var zip = ZipFile.OpenRead(zipPath);
        if (zip.Entries.Count > BackupLimits.MaxArchiveEntries)
        {
            throw new CryptographicException("Jumlah entri arsip melebihi batas aman.");
        }

        foreach (var entry in zip.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrEmpty(entry.FullName) || entry.FullName.EndsWith('/'))
            {
                continue;
            }

            // Strict path traversal & ADS defense (TEST-013, testing.md §6)
            if (entry.FullName.Contains("..", StringComparison.Ordinal) ||
                entry.FullName.Contains(':', StringComparison.Ordinal) ||
                entry.FullName.StartsWith('/') ||
                entry.FullName.StartsWith('\\'))
            {
                throw new CryptographicException("Arsip backup ditolak karena mengandung jalur path traversal ilegal.");
            }

            var normalizedRel = entry.FullName.Replace('/', Path.DirectorySeparatorChar);
            if (ShouldExcludeRelativePath(normalizedRel))
            {
                // Enforce D3 cross-device cookie exclusion even if an older/external archive included them.
                continue;
            }

            var targetPath = Path.GetFullPath(Path.Combine(stagingFull, normalizedRel));
            if (!targetPath.StartsWith(stagingFull, StringComparison.OrdinalIgnoreCase))
            {
                throw new CryptographicException("Arsip backup ditolak karena mencoba menulis di luar direktori staging.");
            }

            var targetDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDir))
            {
                Directory.CreateDirectory(targetDir);
            }

            using var entryStream = entry.Open();
            using var outStream = new FileStream(targetPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

            var buffer = new byte[64 * 1024];
            long entryBytes = 0;
            int read;
            while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                entryBytes += read;
                totalExtractedBytes += read;

                if (entryBytes > BackupLimits.MaxSingleEntryBytes || totalExtractedBytes > BackupLimits.MaxTotalExtractBytes)
                {
                    throw new CryptographicException("Ukuran ekstrak arsip melebihi batas aman (zip-bomb protection).");
                }

                outStream.Write(buffer, 0, read);
            }

            extractedFiles++;
        }

        return extractedFiles;
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
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
