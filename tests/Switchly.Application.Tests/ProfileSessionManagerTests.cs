using Switchly.Application.Browser;
using Switchly.Application.Profiles;
using Switchly.Domain.Profiles;

namespace Switchly.Application.Tests;

public class ProfileSessionManagerTests
{
    [Fact]
    public async Task OpenProfile_CreatesIsolatedContextsPerProfile_AndReusesForSameProfile()
    {
        var profileA = CreateProfile("Profile A", "https://chatgpt.com");
        var profileB = CreateProfile("Profile B", "https://flow.google");
        var store = new FakeSessionStore(profileA, profileB);
        var storage = new FakeStorage();
        var locks = new FakeLockManager();
        var runtime = new FakeBrowserRuntime();

        using var manager = new ProfileSessionManager(store, storage, locks, runtime);

        var ctxA1 = await manager.OpenProfileAsync(profileA.ProfileId);
        var ctxA2 = await manager.OpenProfileAsync(profileA.ProfileId);
        var ctxB = await manager.OpenProfileAsync(profileB.ProfileId);

        Assert.Same(ctxA1, ctxA2);
        Assert.NotSame(ctxA1, ctxB);
        Assert.NotEqual(ctxA1.StoragePath, ctxB.StoragePath);
        Assert.Equal(ProfileRuntimeState.Active, manager.GetState(profileA.ProfileId));
        Assert.Equal(ProfileRuntimeState.Active, manager.GetState(profileB.ProfileId));
        Assert.True(locks.IsLocked(profileA.ProfileId));
        Assert.True(locks.IsLocked(profileB.ProfileId));
        Assert.True(store.LastOpened.ContainsKey(profileA.ProfileId));
    }

    [Fact]
    public async Task OpenProfile_RejectsArchivedProfile()
    {
        var archived = CreateProfile("Archived");
        archived.IsArchived = true;
        var store = new FakeSessionStore(archived);
        var locks = new FakeLockManager();
        using var manager = new ProfileSessionManager(store, new FakeStorage(), locks, new FakeBrowserRuntime());

        await Assert.ThrowsAsync<ProfileArchivedException>(() => manager.OpenProfileAsync(archived.ProfileId));
        Assert.False(locks.IsLocked(archived.ProfileId));
        Assert.Equal(ProfileRuntimeState.Closed, manager.GetState(archived.ProfileId));
    }

    [Fact]
    public async Task OpenProfile_FailureReleasesLockAndSetsErrorState()
    {
        var profile = CreateProfile("Failing Profile");
        var store = new FakeSessionStore(profile);
        var locks = new FakeLockManager();
        var runtime = new FakeBrowserRuntime { FailOnCreateContext = true };
        using var manager = new ProfileSessionManager(store, new FakeStorage(), locks, runtime);

        await Assert.ThrowsAsync<InvalidOperationException>(() => manager.OpenProfileAsync(profile.ProfileId));
        Assert.False(locks.IsLocked(profile.ProfileId));
        Assert.Equal(ProfileRuntimeState.Error, manager.GetState(profile.ProfileId));
    }

    [Fact]
    public async Task CloseLastWindow_SuspendsOnlyTargetProfile_AndAllowsReopen()
    {
        var profileA = CreateProfile("Profile A", "https://dola.com");
        var profileB = CreateProfile("Profile B", "https://seaart.ai");
        var store = new FakeSessionStore(profileA, profileB);
        var locks = new FakeLockManager();
        var runtime = new FakeBrowserRuntime();
        using var manager = new ProfileSessionManager(store, new FakeStorage(), locks, runtime);

        var winA1 = (FakeWindowHost)await manager.OpenWindowAsync(profileA.ProfileId);
        var winA2 = (FakeWindowHost)await manager.OpenWindowAsync(profileA.ProfileId, "https://chatgpt.com");
        var winB = (FakeWindowHost)await manager.OpenWindowAsync(profileB.ProfileId);

        Assert.Equal("https://dola.com", winA1.Address);
        Assert.Equal("https://chatgpt.com", winA2.Address);
        Assert.Equal(2, manager.GetActiveSessions().Single(s => s.ProfileId == profileA.ProfileId).OpenWindowCount);

        // Closing one of two windows keeps profile A active
        manager.CloseWindow(profileA.ProfileId, winA1);
        Assert.True(winA1.Disposed);
        Assert.Equal(ProfileRuntimeState.Active, manager.GetState(profileA.ProfileId));
        Assert.True(locks.IsLocked(profileA.ProfileId));

        // Closing final window suspends profile A without affecting profile B
        manager.CloseWindow(profileA.ProfileId, winA2);
        Assert.True(winA2.Disposed);
        Assert.Equal(ProfileRuntimeState.Suspended, manager.GetState(profileA.ProfileId));
        Assert.False(locks.IsLocked(profileA.ProfileId));
        Assert.Equal(ProfileRuntimeState.Active, manager.GetState(profileB.ProfileId));
        Assert.False(winB.Disposed);

        // Reopening profile A creates a fresh context cleanly
        var reopenedCtx = await manager.OpenProfileAsync(profileA.ProfileId);
        Assert.Equal(ProfileRuntimeState.Active, manager.GetState(profileA.ProfileId));
        Assert.NotNull(reopenedCtx);
    }

    [Fact]
    public async Task OpenProfileAsync_ConcurrentDoubleOpen_CoalescesWithoutError()
    {
        var profile = CreateProfile("Double Click");
        var slowStore = new FakeSessionStore(profile) { DelayMs = 30 };
        var runtime = new FakeBrowserRuntime();
        using var manager = new ProfileSessionManager(slowStore, new FakeStorage(), new FakeLockManager(), runtime);

        var t1 = manager.OpenProfileAsync(profile.ProfileId);
        var t2 = manager.OpenProfileAsync(profile.ProfileId);
        var results = await Task.WhenAll(t1, t2);

        Assert.Same(results[0], results[1]);
        Assert.Equal(ProfileRuntimeState.Active, manager.GetState(profile.ProfileId));
    }

    private static BrowserProfile CreateProfile(string name, string? startupUrl = null)
    {
        var id = Guid.NewGuid();
        return new BrowserProfile
        {
            ProfileId = id,
            Name = name,
            StartupUrl = startupUrl,
            StorageRelativePath = id.ToString("N"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
            PersistSessionCookies = true,
            RestoreLastSession = true,
        };
    }

    private sealed class FakeSessionStore(params BrowserProfile[] profiles) : IProfileSessionStore
    {
        private readonly Dictionary<Guid, BrowserProfile> _profiles = profiles.ToDictionary(p => p.ProfileId);

        public Dictionary<Guid, DateTimeOffset> LastOpened { get; } = [];

        public int DelayMs { get; init; }

        public async Task<BrowserProfile?> GetProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
        {
            if (DelayMs > 0)
            {
                await Task.Delay(DelayMs, cancellationToken);
            }

            return _profiles.TryGetValue(profileId, out var p) ? p : null;
        }

        public Task RecordLastOpenedAsync(Guid profileId, DateTimeOffset openedAtUtc, CancellationToken cancellationToken = default)
        {
            LastOpened[profileId] = openedAtUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeStorage : IProfileStorageDirectory
    {
        public string EnsureCreated(string storageRelativePath) => $@"C:\Profiles\{storageRelativePath}";

        public void DeleteIfExists(string storageRelativePath)
        {
        }

        public void ProcessPendingDeletes()
        {
        }
    }

    private sealed class FakeLockManager : IProfileLockManager
    {
        private readonly HashSet<Guid> _locked = [];

        public bool TryAcquire(Guid profileId) => _locked.Add(profileId);

        public void Release(Guid profileId) => _locked.Remove(profileId);

        public bool IsLocked(Guid profileId) => _locked.Contains(profileId);
    }

    private sealed class FakeBrowserRuntime : IBrowserRuntime
    {
        public bool FailOnCreateContext { get; set; }

        public bool Initialize(string rootCachePath, string? logDirectory = null) => true;

        public void Shutdown()
        {
        }

        public IBrowserProfileContext CreateProfileContext(Guid profileId, string storagePath, bool persistSessionCookies = true)
        {
            if (FailOnCreateContext)
            {
                throw new InvalidOperationException("Simulated CEF context error.");
            }

            return new FakeContext(profileId, storagePath);
        }

        public IBrowserWindowHost CreateWindowHost(IBrowserProfileContext profileContext, string initialUrl)
            => new FakeWindowHost(initialUrl);
    }

    private sealed class FakeContext(Guid profileId, string storagePath) : IBrowserProfileContext
    {
        public Guid ProfileId { get; } = profileId;

        public string StoragePath { get; } = storagePath;

        public bool Disposed { get; private set; }

        public void Dispose() => Disposed = true;
    }

    private sealed class FakeWindowHost(string initialUrl) : IBrowserWindowHost
    {
        public bool CanGoBack { get; private set; }

        public bool CanGoForward { get; private set; }

        public bool IsLoading { get; private set; }

        public string? Address { get; private set; } = initialUrl;

        public string? Title { get; private set; } = "Test Page";

        public bool Disposed { get; private set; }

        public event Action<string>? AddressChanged;

        public event Action<string>? TitleChanged;

        public event Action<bool>? LoadingStateChanged;

        public event Action<string>? NewTabRequested;

        public event Action<DownloadUpdateEvent>? DownloadUpdated;

        public event Action<string>? RendererCrashed;

        public event Action<BrowserShortcutCommand>? ShortcutPressed;

        public event Action<string>? StatusMessageChanged;

        public event Action<int>? ZoomPercentChanged;

        public event Action<bool>? FullscreenModeChanged;

        public void Navigate(string url)
        {
            Address = url;
            CanGoBack = true;
            AddressChanged?.Invoke(url);
        }

        public void GoBack() => CanGoForward = true;

        public void GoForward() => CanGoForward = false;

        public void Reload()
        {
            IsLoading = false;
            LoadingStateChanged?.Invoke(false);
        }

        public void Stop() => IsLoading = false;

        public void ZoomIn()
        {
        }

        public void ZoomOut()
        {
        }

        public void ResetZoom() => ZoomPercentChanged?.Invoke(100);

        public void Find(string searchText, bool forward, bool matchCase, bool findNext)
        {
        }

        public void StopFinding(bool clearSelection) => StatusMessageChanged?.Invoke(string.Empty);

        public Task<bool> FillLoginFormAsync(string username, string password, string? expectedOrigin = null) => Task.FromResult(true);

        public void SimulateRendererCrash(string reason) => RendererCrashed?.Invoke(reason);

        public void SimulateShortcut(BrowserShortcutCommand command) => ShortcutPressed?.Invoke(command);

        public void SimulateTitle(string title)
        {
            Title = title;
            TitleChanged?.Invoke(title);
        }

        public void SimulatePopup(string url) => NewTabRequested?.Invoke(url);

        public void SimulateDownload(DownloadUpdateEvent evt) => DownloadUpdated?.Invoke(evt);

        public void SimulateFullscreen(bool isFullscreen) => FullscreenModeChanged?.Invoke(isFullscreen);

        public void Dispose() => Disposed = true;
    }
}
