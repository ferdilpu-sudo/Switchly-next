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
/// Coordinates per-profile BrowserWindow instances: focuses existing window on switch
/// (design.md §4.1), supports multiple windows per profile (REQ-005), and keeps
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
        var host = (IWpfBrowserWindowHost)await _sessionManager.OpenWindowAsync(profile.ProfileId, initialUrl);
        var window = new BrowserWindow(
            profile,
            host,
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

    private void OnWindowClosed(Guid profileId, IBrowserWindowHost host, BrowserWindow window)
    {
        if (_openWindows.TryGetValue(profileId, out var windows))
        {
            windows.Remove(window);
            if (windows.Count == 0)
            {
                _openWindows.Remove(profileId);
            }
        }

        _sessionManager.CloseWindow(profileId, host);
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
            _logger.LogWarning(ex, "Failed to sync auto WebsiteDomain group for profile {ProfileId}.", profileId);
        }
    }
}
