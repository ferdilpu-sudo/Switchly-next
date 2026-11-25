using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchly.App.Views;
using Switchly.Application.Browser;
using Switchly.Application.Groups;
using Switchly.BrowserHost.BrowserAdapters;
using Switchly.Domain.Profiles;

namespace Switchly.App.Navigation;

public sealed record DockedWorkspaceInfo(
    Guid ProfileId,
    Guid? WorkspaceGroupId,
    string ProfileName,
    string? WorkspaceGroupName,
    string ColorHex,
    bool IsCurrent);

/// <summary>
/// Coordinates Hybrid Workspace sessions (D7, D11):
/// - Default 1-click opens/switches a docked BrowserWorkspaceView inside MainWindow (other active profiles stay alive).
/// - Optional Pop-out opens an independent BrowserWindow sharing the same isolated profile context (REQ-005).
/// </summary>
public sealed class BrowserWindowCoordinator
{
    private readonly record struct WorkspaceKey(Guid ProfileId, Guid? GroupId);

    private readonly IProfileSessionManager _sessionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BrowserWindowCoordinator> _logger;
    private readonly Dictionary<WorkspaceKey, BrowserWorkspaceView> _dockedWorkspaces = [];
    private readonly Dictionary<Guid, List<BrowserWindow>> _openWindows = [];
    private WorkspaceKey? _activeDockedKey;

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

    public event Action? DockedWorkspacesChanged;

    public bool HasOpenPopoutWindow(Guid profileId)
        => _openWindows.TryGetValue(profileId, out var list) && list.Count > 0;

    public IReadOnlyList<DockedWorkspaceInfo> GetOpenDockedWorkspaces()
    {
        return _dockedWorkspaces.Select(kvp => new DockedWorkspaceInfo(
            ProfileId: kvp.Key.ProfileId,
            WorkspaceGroupId: kvp.Key.GroupId,
            ProfileName: kvp.Value.ProfileName,
            WorkspaceGroupName: kvp.Value.WorkspaceGroupName,
            ColorHex: kvp.Value.ProfileColorHex,
            IsCurrent: _activeDockedKey == kvp.Key)).ToList();
    }

    public void FocusDockedWorkspace(Guid profileId, Guid? workspaceGroupId)
    {
        var key = new WorkspaceKey(profileId, workspaceGroupId);
        if (_dockedWorkspaces.TryGetValue(key, out var existing))
        {
            _activeDockedKey = key;
            ActiveDockedWorkspaceChanged?.Invoke(existing);
            DockedWorkspacesChanged?.Invoke();
        }
    }

    public async Task CloseDockedWorkspaceAsync(Guid profileId, Guid? workspaceGroupId)
    {
        var key = new WorkspaceKey(profileId, workspaceGroupId);
        if (_dockedWorkspaces.TryGetValue(key, out var docked))
        {
            if (key.GroupId is null)
            {
                PersistTabs(profileId, docked.WindowId, docked.CaptureOpenTabs());
            }

            _dockedWorkspaces.Remove(key);
            docked.DisposeAllTabs();

            if (_activeDockedKey == key)
            {
                ShowDashboard();
            }

            var hasRemainingSurface =
                _dockedWorkspaces.Keys.Any(k => k.ProfileId == profileId) ||
                HasOpenPopoutWindow(profileId);

            if (!hasRemainingSurface)
            {
                await _sessionManager.CloseProfileAsync(profileId);
            }

            DockedWorkspacesChanged?.Invoke();
        }
    }

    public void ShowDashboard()
    {
        _activeDockedKey = null;
        ActiveDockedWorkspaceChanged?.Invoke(null);
        DockedWorkspacesChanged?.Invoke();
    }

    /// <summary>
    /// Opens or activates a docked BrowserWorkspaceView inside MainWindow (D7, D11):
    /// - When a Web-Space Group (<paramref name="workspaceGroupId"/> + <paramref name="targetUrl"/>) is active,
    ///   creates/switches to a dedicated tab space keyed by (ProfileId, GroupId) starting strictly at targetUrl,
    ///   while sharing the profile's isolated login session/storage.
    /// - Otherwise opens the profile's general browsing workspace.
    /// </summary>
    public async Task OpenOrFocusProfileAsync(
        BrowserProfile profile,
        Guid? workspaceGroupId = null,
        string? workspaceGroupName = null,
        string? targetUrl = null)
    {
        var isWebSpace = workspaceGroupId.HasValue && !string.IsNullOrWhiteSpace(targetUrl);
        var key = new WorkspaceKey(profile.ProfileId, isWebSpace ? workspaceGroupId : null);

        if (_dockedWorkspaces.TryGetValue(key, out var existingDocked))
        {
            _activeDockedKey = key;
            ActiveDockedWorkspaceChanged?.Invoke(existingDocked);
            DockedWorkspacesChanged?.Invoke();
            return;
        }

        List<string> urlsToOpen;
        if (isWebSpace)
        {
            // Dedicated Web-Space opens strictly the group's web URL without mixing general browsing tabs.
            urlsToOpen = [targetUrl!];
        }
        else
        {
            var hasAnyGeneralSurface =
                _dockedWorkspaces.ContainsKey(new WorkspaceKey(profile.ProfileId, null)) ||
                (_openWindows.TryGetValue(profile.ProfileId, out var popouts) && popouts.Count > 0);
            urlsToOpen = await ResolveInitialUrlsAsync(profile, isFirstSurface: !hasAnyGeneralSurface, initialUrl: targetUrl);
        }

        var initialHost = (IWpfBrowserWindowHost)await _sessionManager.OpenWindowAsync(profile.ProfileId, urlsToOpen[0]);

        var workspace = new BrowserWorkspaceView(
            profile,
            initialHost,
            urlsToOpen[0],
            async (profileId, url) => (IWpfBrowserWindowHost)await _sessionManager.OpenWindowAsync(profileId, url),
            (profileId, host) => _sessionManager.CloseWindow(profileId, host),
            p => OpenNewWindowForProfileAsync(p, isWebSpace ? targetUrl : null),
            SyncDomainGroupAsync,
            OnDockedWorkspaceAllTabsClosed,
            onBackToDashboardCallback: ShowDashboard,
            workspaceGroupId: key.GroupId,
            workspaceGroupName: isWebSpace ? workspaceGroupName : null);

        _dockedWorkspaces[key] = workspace;
        _activeDockedKey = key;
        ActiveDockedWorkspaceChanged?.Invoke(workspace);
        DockedWorkspacesChanged?.Invoke();

        for (var i = 1; i < urlsToOpen.Count; i++)
        {
            await workspace.AddRestoredTabAsync(urlsToOpen[i]);
        }
    }

    /// <summary>Opens an independent pop-out BrowserWindow for side-by-side or multi-monitor browsing (REQ-005).</summary>
    public async Task OpenNewWindowForProfileAsync(BrowserProfile profile, string? initialUrl = null)
    {
        var hasAnySurface =
            _dockedWorkspaces.Keys.Any(k => k.ProfileId == profile.ProfileId) ||
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
        var matchingKeys = _dockedWorkspaces.Keys.Where(k => k.ProfileId == profileId).ToList();
        foreach (var key in matchingKeys)
        {
            if (_dockedWorkspaces.TryGetValue(key, out var docked))
            {
                if (key.GroupId is null)
                {
                    PersistTabs(profileId, docked.WindowId, docked.CaptureOpenTabs());
                }

                _dockedWorkspaces.Remove(key);
                docked.DisposeAllTabs();
            }
        }

        if (_activeDockedKey?.ProfileId == profileId)
        {
            ShowDashboard();
        }

        if (_openWindows.TryGetValue(profileId, out var windows))
        {
            foreach (var window in windows.ToList())
            {
                window.Close();
            }
        }

        await _sessionManager.CloseProfileAsync(profileId);
        DockedWorkspacesChanged?.Invoke();
    }

    public void CloseAllWindows()
    {
        foreach (var (key, docked) in _dockedWorkspaces.ToList())
        {
            if (key.GroupId is null)
            {
                PersistTabs(key.ProfileId, docked.WindowId, docked.CaptureOpenTabs());
            }

            docked.DisposeAllTabs();
        }

        _dockedWorkspaces.Clear();
        _activeDockedKey = null;

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
        var key = new WorkspaceKey(workspace.ProfileId, workspace.WorkspaceGroupId);
        if (key.GroupId is null)
        {
            PersistTabs(workspace.ProfileId, workspace.WindowId, workspace.CaptureOpenTabs());
        }

        _dockedWorkspaces.Remove(key);
        workspace.DisposeAllTabs();

        if (_activeDockedKey == key)
        {
            ShowDashboard();
        }
        else
        {
            DockedWorkspacesChanged?.Invoke();
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
