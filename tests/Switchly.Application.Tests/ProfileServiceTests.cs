using Switchly.Application.Profiles;
using Switchly.Domain.Profiles;

namespace Switchly.Application.Tests;

public class ProfileServiceTests
{
    [Fact]
    public async Task Create_Edit_Favorite_Archive_Delete_Lifecycle_Works()
    {
        var repo = new InMemoryProfileRepository();
        var storage = new FakeProfileStorageDirectory();
        var service = new ProfileService(repo, storage);

        // Create (TEST-002)
        var created = await service.CreateAsync(new CreateProfileCommand(
            Name: "  Account A  ",
            StartupUrl: "https://chatgpt.com",
            ColorHex: "#1b6ef3",
            IconKey: "work"));

        Assert.Equal("Account A", created.Name);
        Assert.Equal("#1B6EF3", created.ColorHex);
        Assert.True(created.PersistSessionCookies);
        Assert.True(created.RestoreLastSession);
        Assert.False(created.IsFavorite);
        Assert.False(created.IsArchived);
        Assert.Contains(created.StorageRelativePath, storage.CreatedPaths);

        // Edit & Favorite preserve ProfileId and StorageRelativePath (TEST-023)
        var originalId = created.ProfileId;
        var originalPath = created.StorageRelativePath;

        var updated = await service.UpdateAsync(new UpdateProfileCommand(
            ProfileId: originalId,
            Name: "Account A Renamed",
            StartupUrl: "https://flow.google",
            ColorHex: "#00aa88",
            IconKey: "star",
            IsFavorite: true,
            RestoreLastSession: true,
            PersistSessionCookies: false));

        Assert.Equal(originalId, updated.ProfileId);
        Assert.Equal(originalPath, updated.StorageRelativePath);
        Assert.True(updated.IsFavorite);
        Assert.Equal("#00AA88", updated.ColorHex);
        Assert.Equal("https://flow.google/", updated.StartupUrl);
        Assert.False(updated.PersistSessionCookies);

        // Archive hides from default listing
        await service.SetArchivedAsync(originalId, true);
        Assert.Empty(await service.ListAsync(includeArchived: false));
        Assert.Single(await service.ListAsync(includeArchived: true));

        // Active profile cannot be deleted (rules.md §3)
        await Assert.ThrowsAsync<ProfileInUseException>(() => service.DeleteAsync(originalId, isProfileActive: true));

        // Inactive profile deletes metadata and storage
        await service.DeleteAsync(originalId, isProfileActive: false);
        Assert.Empty(await service.ListAsync(includeArchived: true));
        Assert.DoesNotContain(originalPath, storage.CreatedPaths);
    }

    [Fact]
    public async Task DuplicateConfiguration_CreatesDistinctStorageAndDoesNotCopySessionData()
    {
        var repo = new InMemoryProfileRepository();
        var storage = new FakeProfileStorageDirectory();
        var service = new ProfileService(repo, storage);

        var source = await service.CreateAsync(new CreateProfileCommand(
            Name: "Primary",
            StartupUrl: "https://seaart.ai",
            ColorHex: "#ff0055",
            IconKey: "palette",
            IsFavorite: true,
            RestoreLastSession: true,
            PersistSessionCookies: true));

        // Simulate session artifact inside source storage
        storage.SimulatedFiles[source.StorageRelativePath] = ["Cookies", "Local Storage"];

        var copy = await service.DuplicateConfigurationAsync(new DuplicateProfileCommand(source.ProfileId));

        Assert.NotEqual(source.ProfileId, copy.ProfileId);
        Assert.NotEqual(source.StorageRelativePath, copy.StorageRelativePath);
        Assert.Equal("Primary (Copy)", copy.Name);
        Assert.Equal(source.StartupUrl, copy.StartupUrl);
        Assert.Equal(source.ColorHex, copy.ColorHex);
        Assert.Equal(source.IconKey, copy.IconKey);
        Assert.False(copy.IsFavorite);
        Assert.Null(copy.LastOpenedAtUtc);
        Assert.False(storage.SimulatedFiles.ContainsKey(copy.StorageRelativePath));
    }

    [Fact]
    public async Task DeleteAsync_RejectsLockedProfileViaLockManager()
    {
        var repo = new InMemoryProfileRepository();
        var storage = new FakeProfileStorageDirectory();
        var lockManager = new FakeProfileLockManager();
        var service = new ProfileService(repo, storage, lockManager);

        var profile = await service.CreateAsync(new CreateProfileCommand("Locked Profile"));
        Assert.True(lockManager.TryAcquire(profile.ProfileId));

        await Assert.ThrowsAsync<ProfileInUseException>(() => service.DeleteAsync(profile.ProfileId));

        lockManager.Release(profile.ProfileId);
        await service.DeleteAsync(profile.ProfileId);
        Assert.Empty(await service.ListAsync(includeArchived: true));
    }

    private sealed class FakeProfileLockManager : IProfileLockManager
    {
        private readonly HashSet<Guid> _locked = [];

        public bool TryAcquire(Guid profileId) => _locked.Add(profileId);

        public void Release(Guid profileId) => _locked.Remove(profileId);

        public bool IsLocked(Guid profileId) => _locked.Contains(profileId);
    }

    private sealed class InMemoryProfileRepository : IProfileRepository
    {
        private readonly Dictionary<Guid, BrowserProfile> _store = [];

        public Task<BrowserProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(profileId, out var profile) ? profile : null);

        public Task<IReadOnlyList<BrowserProfile>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        {
            var list = _store.Values
                .Where(p => includeArchived || !p.IsArchived)
                .ToList();
            return Task.FromResult<IReadOnlyList<BrowserProfile>>(list);
        }

        public Task AddAsync(BrowserProfile profile, CancellationToken cancellationToken = default)
        {
            _store[profile.ProfileId] = profile;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(BrowserProfile profile, CancellationToken cancellationToken = default)
        {
            _store[profile.ProfileId] = profile;
            return Task.CompletedTask;
        }

        public Task DeleteAsync(BrowserProfile profile, CancellationToken cancellationToken = default)
        {
            _store.Remove(profile.ProfileId);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProfileStorageDirectory : IProfileStorageDirectory
    {
        public HashSet<string> CreatedPaths { get; } = new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, List<string>> SimulatedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);

        public string EnsureCreated(string storageRelativePath)
        {
            CreatedPaths.Add(storageRelativePath);
            return storageRelativePath;
        }

        public void DeleteIfExists(string storageRelativePath)
        {
            CreatedPaths.Remove(storageRelativePath);
            SimulatedFiles.Remove(storageRelativePath);
        }

        public void ProcessPendingDeletes()
        {
        }
    }
}
