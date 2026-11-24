using Switchly.Infrastructure.Persistence;

namespace Switchly.Infrastructure.Tests;

public class ProfileLockingTests
{
    [Fact]
    public void SingleInstanceMutexGuard_BlocksSecondAcquisition_AndAllowsAfterDispose()
    {
        var root = Path.Combine(Path.GetTempPath(), $"switchly-mutex-{Guid.NewGuid():N}");

        using (var first = new SingleInstanceMutexGuard(root))
        {
            Assert.True(first.TryAcquire());

            using var second = new SingleInstanceMutexGuard(root);
            Assert.False(second.TryAcquire());
        }

        using var third = new SingleInstanceMutexGuard(root);
        Assert.True(third.TryAcquire());
    }

    [Fact]
    public void InProcessProfileLockManager_PreventsDoubleLock_AndReleasesCleanly()
    {
        var manager = new InProcessProfileLockManager();
        var profileId = Guid.NewGuid();

        Assert.False(manager.IsLocked(profileId));
        Assert.True(manager.TryAcquire(profileId));
        Assert.True(manager.IsLocked(profileId));
        Assert.False(manager.TryAcquire(profileId));

        manager.Release(profileId);
        Assert.False(manager.IsLocked(profileId));
        Assert.True(manager.TryAcquire(profileId));
    }

    [Fact]
    public void LocalProfileStorageDirectory_DeferredDeleteQueue_DeletesLockedDirectoryOnNextStartup()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"switchly-tombstone-{Guid.NewGuid():N}");
        var profilesRoot = Path.Combine(tempRoot, "Profiles");
        var dataRoot = Path.Combine(tempRoot, "Data");
        Directory.CreateDirectory(profilesRoot);
        Directory.CreateDirectory(dataRoot);

        try
        {
            var storage = new LocalProfileStorageDirectory(profilesRoot, dataRoot);
            var relativePath = Guid.NewGuid().ToString("N");
            var profileDir = storage.EnsureCreated(relativePath);
            var lockFile = Path.Combine(profileDir, "LOCK");

            // Hold an exclusive file handle to simulate Chromium retaining a handle until exit.
            using (var stream = new FileStream(lockFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                stream.WriteByte(1);
                stream.Flush();

                storage.DeleteIfExists(relativePath);

                // Directory still exists while locked, and tombstone file was queued.
                Assert.True(Directory.Exists(profileDir));
                Assert.True(File.Exists(Path.Combine(dataRoot, "pending-profile-deletes.txt")));
            }

            // Simulate next app startup before Cef.Initialize().
            storage.ProcessPendingDeletes();

            Assert.False(Directory.Exists(profileDir));
            Assert.False(File.Exists(Path.Combine(dataRoot, "pending-profile-deletes.txt")));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }
}
