using System.Windows;
using Switchly.Application.Browser;
using Switchly.BrowserHost.BrowserAdapters;
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>
/// Pop-out isolated browser window bound permanently to one ProfileId (UI-003, REQ-001, REQ-005).
/// Hosts a reusable BrowserWorkspaceView.
/// </summary>
public partial class BrowserWindow : Window
{
    private readonly Action<Guid, BrowserWindow> _onWindowClosedCallback;
    private bool _closedNotified;
    private bool _isVideoFullscreen;
    private WindowStyle _savedWindowStyle = WindowStyle.SingleBorderWindow;
    private WindowState _savedWindowState = WindowState.Normal;
    private ResizeMode _savedResizeMode = ResizeMode.CanResize;

    public BrowserWindow(
        BrowserProfile profile,
        IWpfBrowserWindowHost initialHost,
        string initialUrl,
        Func<Guid, string?, Task<IWpfBrowserWindowHost>> createTabHostCallback,
        Action<Guid, IBrowserWindowHost> closeTabHostCallback,
        Func<BrowserProfile, Task> openNewWindowCallback,
        Func<Guid, string, Task> onUrlVisitedCallback,
        Action<Guid, BrowserWindow> onWindowClosedCallback)
    {
        InitializeComponent();

        _onWindowClosedCallback = onWindowClosedCallback;
        Title = $"Switchly — {profile.Name}";

        Workspace = new BrowserWorkspaceView(
            profile,
            initialHost,
            initialUrl,
            createTabHostCallback,
            closeTabHostCallback,
            openNewWindowCallback,
            onUrlVisitedCallback,
            onAllTabsClosedCallback: _ => Close(),
            onBackToDashboardCallback: null);

        Workspace.ActiveTitleChanged += title => Title = title;
        Workspace.FullscreenModeChanged += OnWorkspaceFullscreenModeChanged;
        WorkspaceHost.Content = Workspace;
        Closed += OnBrowserWindowClosed;
    }

    private void OnWorkspaceFullscreenModeChanged(bool isFullscreen)
    {
        if (_isVideoFullscreen == isFullscreen)
        {
            return;
        }

        _isVideoFullscreen = isFullscreen;
        if (isFullscreen)
        {
            _savedWindowStyle = WindowStyle;
            _savedWindowState = WindowState;
            _savedResizeMode = ResizeMode;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            WindowStyle = _savedWindowStyle;
            ResizeMode = _savedResizeMode;
            WindowState = _savedWindowState;
        }
    }

    public BrowserWorkspaceView Workspace { get; }

    public Guid ProfileId => Workspace.ProfileId;

    public string ProfileName => Workspace.ProfileName;

    public string ProfileColorHex => Workspace.ProfileColorHex;

    public Guid WindowId => Workspace.WindowId;

    public IReadOnlyList<TabSnapshotInput> CaptureOpenTabs() => Workspace.CaptureOpenTabs();

    public Task AddRestoredTabAsync(string url) => Workspace.AddRestoredTabAsync(url);

    private void OnBrowserWindowClosed(object? sender, EventArgs e)
    {
        if (_closedNotified)
        {
            return;
        }

        _closedNotified = true;
        _onWindowClosedCallback(ProfileId, this);
        Workspace.DisposeAllTabs();
    }
}
