using Switchly.Application.Profiles;

namespace Switchly.Infrastructure.Persistence;

/// <summary>
/// Manages per-profile storage directories directly under RootCachePath (ADR-011),
/// rejecting nested paths or path-traversal segments, and queuing locked directories
/// for deferred deletion on next startup before Cef.Initialize().
/// </summary>
public sealed class LocalProfileStorageDirectory : IProfileStorageDirectory
{
    private readonly string _rootCachePath;
    private readonly string _tombstoneFilePath;
    private readonly object _tombstoneLock = new();

    public LocalProfileStorageDirectory(string rootCachePath, string? tombstoneDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(rootCachePath))
        {
            throw new ArgumentException("RootCachePath is required.", nameof(rootCachePath));
        }

        _rootCachePath = Path.GetFullPath(rootCachePath)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        var tombstoneRoot = string.IsNullOrWhiteSpace(tombstoneDirectory)
            ? Path.GetDirectoryName(_rootCachePath) ?? _rootCachePath
            : Path.GetFullPath(tombstoneDirectory);
        _tombstoneFilePath = Path.Combine(tombstoneRoot, "pending-profile-deletes.txt");
    }

    public string EnsureCreated(string storageRelativePath)
    {
        var fullPath = ResolveDirectChildPath(storageRelativePath);
        Directory.CreateDirectory(fullPath);
        return fullPath;
    }

    public void DeleteIfExists(string storageRelativePath)
    {
        var fullPath = ResolveDirectChildPath(storageRelativePath);
        if (!Directory.Exists(fullPath))
        {
            return;
        }

        try
        {
            Directory.Delete(fullPath, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Chromium may retain a handle inside <profile-guid> until process exit;
            // queue for deterministic cleanup on next startup before Cef.Initialize().
            EnqueuePendingDelete(storageRelativePath);
        }
    }

    public void ProcessPendingDeletes()
    {
        lock (_tombstoneLock)
        {
            if (!File.Exists(_tombstoneFilePath))
            {
                return;
            }

            var entries = File.ReadAllLines(_tombstoneFilePath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var remaining = new List<string>();
            foreach (var entry in entries)
            {
                string fullPath;
                try
                {
                    fullPath = ResolveDirectChildPath(entry.Trim());
                }
                catch (ArgumentException)
                {
                    // Drop invalid/tampered entries rather than touching anything outside RootCachePath.
                    continue;
                }

                if (!Directory.Exists(fullPath))
                {
                    continue;
                }

                try
                {
                    Directory.Delete(fullPath, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    remaining.Add(entry.Trim());
                }
            }

            if (remaining.Count == 0)
            {
                File.Delete(_tombstoneFilePath);
            }
            else
            {
                File.WriteAllLines(_tombstoneFilePath, remaining);
            }
        }
    }

    public string ResolveDirectChildPath(string storageRelativePath)
    {
        if (string.IsNullOrWhiteSpace(storageRelativePath) ||
            storageRelativePath.Contains(Path.DirectorySeparatorChar) ||
            storageRelativePath.Contains(Path.AltDirectorySeparatorChar) ||
            storageRelativePath.Contains("..", StringComparison.Ordinal) ||
            Path.IsPathRooted(storageRelativePath))
        {
            throw new ArgumentException(
                "StorageRelativePath must be a single safe directory name directly under RootCachePath (ADR-011).",
                nameof(storageRelativePath));
        }

        var combined = Path.GetFullPath(Path.Combine(_rootCachePath, storageRelativePath))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var parent = Path.GetDirectoryName(combined)
            ?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        if (!string.Equals(parent, _rootCachePath, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "Resolved profile path escaped RootCachePath.",
                nameof(storageRelativePath));
        }

        return combined;
    }

    private void EnqueuePendingDelete(string storageRelativePath)
    {
        lock (_tombstoneLock)
        {
            var dir = Path.GetDirectoryName(_tombstoneFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.AppendAllLines(_tombstoneFilePath, [storageRelativePath]);
        }
    }
}
