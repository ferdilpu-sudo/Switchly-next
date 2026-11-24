using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchly.App.Views;
using Switchly.Application.Browser;
using Switchly.Application.Groups;
using Switchly.BrowserHost.BrowserAdapters;
using Switchly.Domain.Profiles;

namespace Switchly.App.Navigation;

/// <summary>
/// Coordinates Hybrid Workspace sessions (D7):
/// - Default 1-click opens/switches a docked BrowserWorkspaceView inside MainWindow (other active profiles stay alive).
/// - Optional Pop-out opens an independent BrowserWindow sharing the same isolated profile context (REQ-005).
/// </summary>
public sealed class BrowserWindowCoordinator
{
    private readonly IProfileSessionManager _sessionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BrowserWindowCoordinator> _logger;
    private readonly Dictionary<Guid, BrowserWorkspaceView> _dockedWorkspaces = [];
    private readonly Dictionary<Guid, List<BrowserWindow>> _openWindows = [];
    private Guid? _activeDockedProfileId;

    public BrowserWindowCoordinator(
        IProfileSessionManager sessionManager,
        IServiceScopeFactory scopeFactory,
        ILogger<BrowserWindowCoordinator> logger)
    {
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public event Action? GroupsChanged;

    public event Action<BrowserWorkspaceView?>? ActiveDockedWorkspaceChanged;

    public void ShowDashboard()
    {
        _activeDockedProfileId = null;
        ActiveDockedWorkspaceChanged?.Invoke(null);
    }

    /// <summary>Opens or activates the profile docked inside MainWindow's integrated right pane (D7).</summary>
    public async Task OpenOrFocusProfileAsync(BrowserProfile profile)
    {
        if (_dockedWorkspaces.TryGetValue(profile.ProfileId, out var existingDocked))
        {
            _activeDockedProfileId = profile.ProfileId;
            ActiveDockedWorkspaceChanged?.Invoke(existingDocked);
            return;
        }

        var hasAnySurface = _openWindows.TryGetValue(profile.ProfileId, out var popouts) && popouts.Count > 0;
        var urlsToOpen = await ResolveInitialUrlsAsync(profile, isFirstSurface: !hasAnySurface, initialUrl: null);
        var initialHost = (IWpfBrowserWindowHost)await _sessionManager.OpenWindowAsync(profile.ProfileId, urlsToOpen[0]);

        var workspace = new BrowserWorkspaceView(
            profile,
            initialHost,
            urlsToOpen[0],
            async (profileId, url) => (IWpfBrowserWindowHost)await _sessionManager.OpenWindowAsync(profileId, url),
            (profileId, host) => _sessionManager.CloseWindow(profileId, host),
            p => OpenNewWindowForProfileAsync(p),
            SyncDomainGroupAsync,
            OnDockedWorkspaceAllTabsClosed,
            onBackToDashboardCallback: ShowDashboard);

        _dockedWorkspaces[profile.ProfileId] = workspace;
        _activeDockedProfileId = profile.ProfileId;
        ActiveDockedWorkspaceChanged?.Invoke(workspace);

        for (var i = 1; i < urlsToOpen.Count; i++)
        {
            await workspace.AddRestoredTabAsync(urlsToOpen[i]);
        }
    }

    /// <summary>Opens an independent pop-out BrowserWindow for side-by-side or multi-monitor browsing (REQ-005).</summary>
    public async Task OpenNewWindowForProfileAsync(BrowserProfile profile, string? initialUrl = null)
    {
        var hasAnySurface =
            _dockedWorkspaces.ContainsKey(profile.ProfileId) ||
            (_openWindows.TryGetValue(profile.ProfileId, out var existingList) && existingList.Count > 0);

        var urlsToOpen = await ResolveInitialUrlsAsync(profile, isFirstSurface: !hasAnySurface, initialUrl);
        var initialHost = (IWpfBrowserWindowHost)await _sessionManager.OpenWindowAsync(profile.ProfileId, urlsToOpen[0]);

        var window = new BrowserWindow(
            profile,
            initialHost,
            urlsToOpen[0],
            async (profileId, url) => (IWpfBrowserWindowHost)await _sessionManager.OpenWindowAsync(profileId, url),
            (profileId, host) => _sessionManager.CloseWindow(profileId, host),
            p => OpenNewWindowForProfileAsync(p),
            SyncDomainGroupAsync,
            OnWindowClosed);

        if (!_openWindows.TryGetValue(profile.ProfileId, out var list))
        {
            list = [];
            _openWindows[profile.ProfileId] = list;
        }

        list.Add(window);
        window.Show();

        for (var i = 1; i < urlsToOpen.Count; i++)
        {
            await window.AddRestoredTabAsync(urlsToOpen[i]);
        }
    }

    public async Task CloseProfileWindowsAsync(Guid profileId)
    {
        if (_dockedWorkspaces.TryGetValue(profileId, out var docked))
        {
            PersistTabs(profileId, docked.WindowId, docked.CaptureOpenTabs());
            _dockedWorkspaces.Remove(profileId);
            docked.DisposeAllTabs();

            if (_activeDockedProfileId == profileId)
            {
                ShowDashboard();
            }
        }

        if (_openWindows.TryGetValue(profileId, out var windows))
        {
            foreach (var window in windows.ToList())
            {
                window.Close();
            }
        }

        await _sessionManager.CloseProfileAsync(profileId);
    }

    public void CloseAllWindows()
    {
        foreach (var (profileId, docked) in _dockedWorkspaces.ToList())
        {
            PersistTabs(profileId, docked.WindowId, docked.CaptureOpenTabs());
            docked.DisposeAllTabs();
        }

        _dockedWorkspaces.Clear();
        _activeDockedProfileId = null;

        foreach (var windows in _openWindows.Values.ToList())
        {
            foreach (var window in windows.ToList())
            {
                window.Close();
            }
        }
    }

    private async Task<List<string>> ResolveInitialUrlsAsync(BrowserProfile profile, bool isFirstSurface, string? initialUrl)
    {
        var urlsToOpen = new List<string>();
        if (isFirstSurface && string.IsNullOrWhiteSpace(initialUrl) && profile.RestoreLastSession)
        {
            using var scope = _scopeFactory.CreateScope();
            var tabSessions = scope.ServiceProvider.GetRequiredService<ITabSessionService>();
            var restored = await tabSessions.GetRestorableTabsAsync(profile.ProfileId);
            urlsToOpen.AddRange(restored.Select(t => t.Url));
        }

        if (urlsToOpen.Count == 0)
        {
            urlsToOpen.Add(!string.IsNullOrWhiteSpace(initialUrl)
                ? initialUrl
                : profile.StartupUrl ?? "about:blank");
        }

        return urlsToOpen;
    }

    private void OnDockedWorkspaceAllTabsClosed(BrowserWorkspaceView workspace)
    {
        PersistTabs(workspace.ProfileId, workspace.WindowId, workspace.CaptureOpenTabs());
        _dockedWorkspaces.Remove(workspace.ProfileId);
        workspace.DisposeAllTabs();

        if (_activeDockedProfileId == workspace.ProfileId)
        {
            ShowDashboard();
        }
    }

    private void OnWindowClosed(Guid profileId, BrowserWindow window)
    {
        PersistTabs(profileId, window.WindowId, window.CaptureOpenTabs());

        if (_openWindows.TryGetValue(profileId, out var windows))
        {
            windows.Remove(window);
            if (windows.Count == 0)
            {
                _openWindows.Remove(profileId);
            }
        }
    }

    private void PersistTabs(Guid profileId, Guid windowId, IReadOnlyList<TabSnapshotInput> tabs)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tabSessions = scope.ServiceProvider.GetRequiredService<ITabSessionService>();
            tabSessions.SaveOpenTabsAsync(profileId, windowId, tabs).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist tab session for profile {ProfileId}.", profileId);
        }
    }

    private async Task SyncDomainGroupAsync(Guid profileId, string url)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
            await groupService.SyncWebsiteDomainGroupAsync(profileId, url);
            GroupsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync domain group for profile {ProfileId}.", profileId);
        }
    }
}
