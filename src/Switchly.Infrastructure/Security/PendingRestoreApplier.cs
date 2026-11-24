using System.Text.Json;

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
        catch (Exception ex) when (ex is IOException or JsonException)
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
        var stagingDir = Path.GetFullPath(Path.Combine(stagingRoot, marker.StagingDirectoryName));
        var stagedDb = Path.Combine(stagingDir, "data", "switchly-next.db");
        if (!stagingDir.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(stagedDb))
        {
            TryDeleteFile(markerPath);
            return false;
        }

        var dataDir = Path.Combine(appDataRoot, "Data");
        var liveDb = Path.Combine(dataDir, "switchly-next.db");
        var liveProfilesDir = Path.Combine(appDataRoot, "Profiles");
        var rollbackDir = Path.Combine(appDataRoot, $"RestoreRollback-{Guid.NewGuid():N}");

        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(rollbackDir);

        try
        {
            // 1. Move current live DB and Profiles aside for rollback safety.
            if (File.Exists(liveDb))
            {
                File.Move(liveDb, Path.Combine(rollbackDir, "switchly-next.db"));
            }

            TryDeleteFile(liveDb + "-wal");
            TryDeleteFile(liveDb + "-shm");

            if (Directory.Exists(liveProfilesDir))
            {
                Directory.Move(liveProfilesDir, Path.Combine(rollbackDir, "Profiles"));
            }

            // 2. Promote staged DB and staged Profiles.
            File.Move(stagedDb, liveDb);
            Directory.CreateDirectory(liveProfilesDir);

            var stagedProfilesDir = Path.Combine(stagingDir, "profiles");
            if (Directory.Exists(stagedProfilesDir))
            {
                foreach (var dir in Directory.EnumerateDirectories(stagedProfilesDir))
                {
                    var name = Path.GetFileName(dir);
                    Directory.Move(dir, Path.Combine(liveProfilesDir, name));
                }
            }

            // 3. Cleanup marker, staging, and rollback directory.
            TryDeleteFile(markerPath);
            TryDeleteDirectory(stagingDir);
            TryDeleteDirectory(rollbackDir);
            return true;
        }
        catch
        {
            // Roll back to previous live data so no partial restore state remains (TEST-020).
            try
            {
                TryDeleteFile(liveDb);
                TryDeleteDirectory(liveProfilesDir);

                var rollbackDb = Path.Combine(rollbackDir, "switchly-next.db");
                if (File.Exists(rollbackDb))
                {
                    File.Move(rollbackDb, liveDb);
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
    }
}
