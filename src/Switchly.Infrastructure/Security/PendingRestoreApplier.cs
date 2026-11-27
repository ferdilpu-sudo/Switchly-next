using System.Text.Json;
using Switchly.Application.Vault;

namespace Switchly.Infrastructure.Security;

public sealed record PendingRestoreMarker(
    string StagingDirectoryName,
    int ArchiveVersion,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Executes the restart-to-restore staging swap BEFORE Cef.Initialize() and EF Core open any handles (TASK-033, ADR-006, D3).
/// Automatically rolls back if any filesystem error occurs during swap (TEST-020).
/// </summary>
public static class PendingRestoreApplier
{
    public static string GetMarkerPath(string appDataRoot)
        => Path.Combine(appDataRoot, "Data", "pending-restore.json");

    public static bool ApplyPendingRestoreIfAny(string appDataRoot)
    {
        var markerPath = GetMarkerPath(appDataRoot);
        if (!File.Exists(markerPath))
        {
            return false;
        }

        PendingRestoreMarker? marker;
        try
        {
            marker = JsonSerializer.Deserialize<PendingRestoreMarker>(File.ReadAllText(markerPath));
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            TryDeleteFile(markerPath);
            return false;
        }

        if (marker is null || string.IsNullOrWhiteSpace(marker.StagingDirectoryName) ||
            marker.StagingDirectoryName.Contains("..", StringComparison.Ordinal) ||
            marker.StagingDirectoryName.Contains(Path.DirectorySeparatorChar) ||
            marker.StagingDirectoryName.Contains(Path.AltDirectorySeparatorChar))
        {
            TryDeleteFile(markerPath);
            return false;
        }

        var stagingRoot = Path.GetFullPath(Path.Combine(appDataRoot, "RestoreStaging"));
        if (!stagingRoot.EndsWith(Path.DirectorySeparatorChar))
        {
            stagingRoot += Path.DirectorySeparatorChar;
        }

        var stagingDir = Path.GetFullPath(Path.Combine(stagingRoot, marker.StagingDirectoryName));
        var stagedDb = Path.Combine(stagingDir, "data", "switchly-next.db");
        var stagedVaultKey = Path.Combine(stagingDir, "data", PasswordVaultService.VaultKeyFileName);
        if (!stagingDir.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(stagedDb))
        {
            TryDeleteFile(markerPath);
            return false;
        }

        var dataDir = Path.Combine(appDataRoot, "Data");
        var liveDb = Path.Combine(dataDir, "switchly-next.db");
        var liveVaultKey = Path.Combine(dataDir, PasswordVaultService.VaultKeyFileName);
        var liveProfilesDir = Path.Combine(appDataRoot, "Profiles");
        var rollbackDir = Path.Combine(appDataRoot, $"RestoreRollback-{Guid.NewGuid():N}");

        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(rollbackDir);

        try
        {
            // 1. Move current live DB, Vault Key, and Profiles aside for rollback safety.
            if (File.Exists(liveDb))
            {
                File.Move(liveDb, Path.Combine(rollbackDir, "switchly-next.db"));
            }

            if (File.Exists(liveVaultKey))
            {
                File.Move(liveVaultKey, Path.Combine(rollbackDir, PasswordVaultService.VaultKeyFileName));
            }

            TryDeleteFile(liveDb + "-wal");
            TryDeleteFile(liveDb + "-shm");

            if (Directory.Exists(liveProfilesDir))
            {
                Directory.Move(liveProfilesDir, Path.Combine(rollbackDir, "Profiles"));
            }

            // 2. Promote staged DB, staged Vault Key (if present), and validated staged Profile directories.
            File.Move(stagedDb, liveDb);
            if (File.Exists(stagedVaultKey))
            {
                File.Move(stagedVaultKey, liveVaultKey);
            }

            Directory.CreateDirectory(liveProfilesDir);

            var stagedProfilesDir = Path.Combine(stagingDir, "profiles");
            if (Directory.Exists(stagedProfilesDir))
            {
                foreach (var dir in Directory.EnumerateDirectories(stagedProfilesDir))
                {
                    var name = Path.GetFileName(dir);
                    if (!PortableBackupRestoreService.IsValidProfileDirectorySegment(name))
                    {
                        continue;
                    }

                    Directory.Move(dir, Path.Combine(liveProfilesDir, name));
                }
            }
        }
        catch
        {
            // Roll back to previous live data so no partial restore state remains (TEST-020).
            try
            {
                TryDeleteFile(liveDb);
                TryDeleteFile(liveVaultKey);
                TryDeleteDirectory(liveProfilesDir);

                var rollbackDb = Path.Combine(rollbackDir, "switchly-next.db");
                if (File.Exists(rollbackDb))
                {
                    File.Move(rollbackDb, liveDb);
                }

                var rollbackVaultKey = Path.Combine(rollbackDir, PasswordVaultService.VaultKeyFileName);
                if (File.Exists(rollbackVaultKey))
                {
                    File.Move(rollbackVaultKey, liveVaultKey);
                }

                var rollbackProfiles = Path.Combine(rollbackDir, "Profiles");
                if (Directory.Exists(rollbackProfiles))
                {
                    Directory.Move(rollbackProfiles, liveProfilesDir);
                }
            }
            catch
            {
                // Preserve rollbackDir for manual recovery if rollback itself fails.
            }

            TryDeleteFile(markerPath);
            throw;
        }

        // 3. Post-commit cleanup MUST run outside the rollback try/catch so cleanup failures never destroy committed live data (SEC-03).
        TryDeleteFile(markerPath);
        TryDeleteDirectory(stagingDir);
        TryDeleteDirectory(rollbackDir);
        return true;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.SetAttributes(path, FileAttributes.Normal);
                File.Delete(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                }
                catch
                {
                }
            }

            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
