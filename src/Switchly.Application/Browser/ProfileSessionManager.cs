using Switchly.Application.Profiles;
using Switchly.Domain.Profiles;

namespace Switchly.Application.Browser;

/// <summary>
/// Manages per-profile BrowserHost contexts, window counts, and lifecycle transitions
/// (Closed -> Starting -> Active -> Suspending -> Suspended / Error).
/// Guarantees one unique isolated IBrowserProfileContext per active ProfileId (REQ-003, REQ-005).
/// </summary>
public sealed class ProfileSessionManager : IProfileSessionManager
{
    private const string DefaultBlankUrl = "about:blank";

    private readonly IProfileSessionStore _store;
    private readonly IProfileStorageDirectory _storage;
    private readonly IProfileLockManager _lockManager;
    private readonly IBrowserRuntime _runtime;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();
    private readonly Dictionary<Guid, ActiveProfileSession> _sessions = [];
    private readonly Dictionary<Guid, Task<IBrowserProfileContext>> _inFlightOpens = [];
    private readonly HashSet<Guid> _closingProfiles = [];
    private readonly Dictionary<Guid, ProfileRuntimeState> _states = [];
    private bool _disposed;

    public ProfileSessionManager(
        IProfileSessionStore store,
        IProfileStorageDirectory storage,
        IProfileLockManager lockManager,
        IBrowserRuntime runtime,
        TimeProvider? timeProvider = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _lockManager = lockManager ?? throw new ArgumentNullException(nameof(lockManager));
        _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public event Action<ProfileSessionSnapshot>? StateChanged;

    public Task<IBrowserProfileContext> OpenProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        cancellationToken.ThrowIfCancellationRequested();

        Task<IBrowserProfileContext> sharedTask;
        lock (_gate)
        {
            if (_sessions.TryGetValue(profileId, out var existing))
            {
                return Task.FromResult(existing.Context);
            }

            if (_inFlightOpens.TryGetValue(profileId, out var inFlight))
            {
                sharedTask = inFlight;
            }
            else
            {
                _closingProfiles.Remove(profileId);
                sharedTask = OpenProfileCoreAsync(profileId);
                if (!sharedTask.IsCompleted)
                {
                    _inFlightOpens[profileId] = sharedTask;
                }
            }
        }

        return cancellationToken.CanBeCanceled
            ? sharedTask.WaitAsync(cancellationToken)
            : sharedTask;
    }

    private async Task<IBrowserProfileContext> OpenProfileCoreAsync(Guid profileId)
    {
        try
        {
            var profile = await _store.GetProfileAsync(profileId, CancellationToken.None)
                ?? throw new ProfileNotFoundException(profileId);

            if (profile.IsArchived)
            {
                throw new ProfileArchivedException(profileId);
            }

            if (!_lockManager.TryAcquire(profileId))
            {
                throw new ProfileInUseException(profileId);
            }

            IBrowserProfileContext? context = null;
            try
            {
                SetState(profileId, ProfileRuntimeState.Starting, openWindowCount: 0);

                var fullStoragePath = _storage.EnsureCreated(profile.StorageRelativePath);
                context = _runtime.CreateProfileContext(profile.ProfileId, fullStoragePath, profile.PersistSessionCookies);

                await _store.RecordLastOpenedAsync(profileId, _timeProvider.GetUtcNow(), CancellationToken.None);

                lock (_gate)
                {
                    if (_disposed || _closingProfiles.Remove(profileId))
                    {
                        throw new OperationCanceledException($"Profile '{profileId}' was closed during startup.");
                    }

                    _sessions[profileId] = new ActiveProfileSession(profile, context);
                }

                SetState(profileId, ProfileRuntimeState.Active, openWindowCount: 0);
                return context;
            }
            catch (Exception ex)
            {
                context?.Dispose();
                _lockManager.Release(profileId);
                var nextState = ex is OperationCanceledException ? ProfileRuntimeState.Closed : ProfileRuntimeState.Error;
                SetState(profileId, nextState, openWindowCount: 0, lastError: ex is OperationCanceledException ? null : ex.GetType().Name);
                throw;
            }
        }
        finally
        {
            lock (_gate)
            {
                _inFlightOpens.Remove(profileId);
            }
        }
    }

    public async Task<IBrowserWindowHost> OpenWindowAsync(
        Guid profileId,
        string? initialUrl = null,
        CancellationToken cancellationToken = default)
    {
        var context = await OpenProfileAsync(profileId, cancellationToken);

        string resolvedUrl;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(profileId, out var session))
            {
                throw new InvalidOperationException($"Profile '{profileId}' was closed before window creation completed.");
            }

            resolvedUrl = !string.IsNullOrWhiteSpace(initialUrl)
                ? initialUrl
                : session.Profile.StartupUrl ?? DefaultBlankUrl;
        }

        IBrowserWindowHost windowHost;
        try
        {
            windowHost = _runtime.CreateWindowHost(context, resolvedUrl);
        }
        catch
        {
            ActiveProfileSession? orphanToClose = null;
            lock (_gate)
            {
                if (_sessions.TryGetValue(profileId, out var s) && s.Windows.Count == 0)
                {
                    _sessions.Remove(profileId);
                    orphanToClose = s;
                }
            }

            if (orphanToClose is not null)
            {
                orphanToClose.Context.Dispose();
                _lockManager.Release(profileId);
                SetState(profileId, ProfileRuntimeState.Error, openWindowCount: 0, lastError: "WindowCreationFailed");
            }

            throw;
        }

        int count;
        lock (_gate)
        {
            if (!_sessions.TryGetValue(profileId, out var session))
            {
                windowHost.Dispose();
                throw new InvalidOperationException($"Profile '{profileId}' closed concurrently.");
            }

            session.Windows.Add(windowHost);
            count = session.Windows.Count;
        }

        SetState(profileId, ProfileRuntimeState.Active, count);
        return windowHost;
    }

    public void CloseWindow(Guid profileId, IBrowserWindowHost windowHost)
    {
        ArgumentNullException.ThrowIfNull(windowHost);

        ActiveProfileSession? sessionToSuspend = null;
        int remainingWindows = 0;

        lock (_gate)
        {
            if (!_sessions.TryGetValue(profileId, out var session) || !session.Windows.Remove(windowHost))
            {
                windowHost.Dispose();
                return;
            }

            remainingWindows = session.Windows.Count;

            if (remainingWindows == 0)
            {
                _sessions.Remove(profileId);
                sessionToSuspend = session;
            }
        }

        windowHost.Dispose();

        if (sessionToSuspend is not null)
        {
            SetState(profileId, ProfileRuntimeState.Suspending, openWindowCount: 0);
            sessionToSuspend.Context.Dispose();
            _lockManager.Release(profileId);
            SetState(profileId, ProfileRuntimeState.Suspended, openWindowCount: 0);
        }
        else
        {
            SetState(profileId, ProfileRuntimeState.Active, remainingWindows);
        }
    }

    public async Task CloseProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        Task<IBrowserProfileContext>? inFlightToDrain;
        lock (_gate)
        {
            if (_inFlightOpens.TryGetValue(profileId, out inFlightToDrain))
            {
                _closingProfiles.Add(profileId);
            }
        }

        if (inFlightToDrain is not null)
        {
            try
            {
                await inFlightToDrain.ConfigureAwait(false);
            }
            catch
            {
                // Expected when cancelled/faulted during startup; drain before proceeding.
            }
        }

        ActiveProfileSession? sessionToClose;
        List<IBrowserWindowHost> windowsToDispose;

        lock (_gate)
        {
            _closingProfiles.Remove(profileId);
            if (!_sessions.TryGetValue(profileId, out sessionToClose))
            {
                SetState(profileId, ProfileRuntimeState.Closed, openWindowCount: 0);
                return;
            }

            _sessions.Remove(profileId);
            windowsToDispose = [.. sessionToClose.Windows];
            sessionToClose.Windows.Clear();
        }

        SetState(profileId, ProfileRuntimeState.Suspending, openWindowCount: 0);

        foreach (var window in windowsToDispose)
        {
            window.Dispose();
        }

        sessionToClose.Context.Dispose();
        _lockManager.Release(profileId);
        SetState(profileId, ProfileRuntimeState.Closed, openWindowCount: 0);
    }

    public ProfileRuntimeState GetState(Guid profileId)
    {
        lock (_gate)
        {
            return _states.TryGetValue(profileId, out var state)
                ? state
                : ProfileRuntimeState.Closed;
        }
    }

    public IReadOnlyList<ProfileSessionSnapshot> GetActiveSessions()
    {
        lock (_gate)
        {
            return _sessions
                .Select(kvp => new ProfileSessionSnapshot(
                    kvp.Key,
                    ProfileRuntimeState.Active,
                    kvp.Value.Windows.Count))
                .ToList();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        List<Guid> idsToClose;
        lock (_gate)
        {
            idsToClose = [.. _sessions.Keys.Union(_inFlightOpens.Keys)];
        }

        foreach (var id in idsToClose)
        {
            CloseProfileAsync(id).GetAwaiter().GetResult();
        }
    }

    private void SetState(Guid profileId, ProfileRuntimeState state, int openWindowCount, string? lastError = null)
    {
        lock (_gate)
        {
            _states[profileId] = state;
        }

        StateChanged?.Invoke(new ProfileSessionSnapshot(profileId, state, openWindowCount, lastError));
    }

    private sealed class ActiveProfileSession(BrowserProfile profile, IBrowserProfileContext context)
    {
        public BrowserProfile Profile { get; } = profile;
        public IBrowserProfileContext Context { get; } = context;
        public List<IBrowserWindowHost> Windows { get; } = [];
    }
}
