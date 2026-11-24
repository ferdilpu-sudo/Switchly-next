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
/// Coordinates per-profile multi-tab BrowserWindow instances: focuses existing window on switch
/// (design.md §4.1), supports multiple windows and tabs per profile (REQ-005), and keeps
/// profiles strictly isolated (REQ-003).
/// </summary>
public sealed class BrowserWindowCoordinator
{
    private readonly IProfileSessionManager _sessionManager;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BrowserWindowCoordinator> _logger;
    private readonly Dictionary<Guid, List<BrowserWindow>> _openWindows = [];

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

    public async Task OpenOrFocusProfileAsync(BrowserProfile profile)
    {
        if (_openWindows.TryGetValue(profile.ProfileId, out var windows) && windows.Count > 0)
        {
            var lastWindow = windows[^1];
            if (lastWindow.WindowState == WindowState.Minimized)
            {
                lastWindow.WindowState = WindowState.Normal;
            }

            lastWindow.Activate();
            return;
        }

        await OpenNewWindowForProfileAsync(profile);
    }

    public async Task OpenNewWindowForProfileAsync(BrowserProfile profile, string? initialUrl = null)
    {
        var isFirstWindow = !_openWindows.TryGetValue(profile.ProfileId, out var existingList) || existingList.Count == 0;
        var urlsToOpen = new List<string>();

        if (isFirstWindow && string.IsNullOrWhiteSpace(initialUrl) && profile.RestoreLastSession)
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
        foreach (var windows in _openWindows.Values.ToList())
        {
            foreach (var window in windows.ToList())
            {
                window.Close();
            }
        }
    }

    private void OnWindowClosed(Guid profileId, BrowserWindow window)
    {
        var tabs = window.CaptureOpenTabs();
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var tabSessions = scope.ServiceProvider.GetRequiredService<ITabSessionService>();
            tabSessions.SaveOpenTabsAsync(profileId, window.WindowId, tabs).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist tab session for profile {ProfileId}.", profileId);
        }

        if (_openWindows.TryGetValue(profileId, out var windows))
        {
            windows.Remove(window);
            if (windows.Count == 0)
            {
                _openWindows.Remove(profileId);
            }
        }
    }

    private async Task SyncDomainGroupAsync(Guid profileId, string url)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
            await groupService.SyncWebsiteDomainGroupAsync(profileId, url);

            var historyService = scope.ServiceProvider.GetRequiredService<IHistoryService>();
            await historyService.RecordVisitAsync(profileId, url);

            GroupsChanged?.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to sync domain group / history for profile {ProfileId}.", profileId);
        }
    }
}
