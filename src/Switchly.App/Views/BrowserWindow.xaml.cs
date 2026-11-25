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
        WorkspaceHost.Content = Workspace;
        Closed += OnBrowserWindowClosed;
    }

    public BrowserWorkspaceView Workspace { get; }

    public Guid ProfileId => Workspace.ProfileId;

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
