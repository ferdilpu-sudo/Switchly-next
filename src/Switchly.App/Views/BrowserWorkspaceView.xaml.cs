using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Switchly.App.ViewModels;
using Switchly.Application.Browser;
using Switchly.Application.Vault;
using Switchly.BrowserHost.BrowserAdapters;
using Switchly.Domain.Browser;
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>
/// Reusable multi-tab isolated browser surface bound to one ProfileId.
/// Hosted either docked inside MainWindow (Hybrid Workspace) or popped out inside BrowserWindow.
/// </summary>
public partial class BrowserWorkspaceView : UserControl
{
    private readonly BrowserProfile _profile;
    private readonly Func<Guid, string?, Task<IWpfBrowserWindowHost>> _createTabHostCallback;
    private readonly Action<Guid, IBrowserWindowHost> _closeTabHostCallback;
    private readonly Func<BrowserProfile, Task> _openNewWindowCallback;
    private readonly Func<Guid, string, Task> _onUrlVisitedCallback;
    private readonly Action<BrowserWorkspaceView> _onAllTabsClosedCallback;
    private readonly Action? _onBackToDashboardCallback;
    private readonly ObservableCollection<BrowserTabItemViewModel> _tabs = [];
    private readonly Stack<string> _recentlyClosedUrls = new();
    private BrowserTabItemViewModel? _activeTab;
    private bool _disposed;

    public BrowserWorkspaceView(
        BrowserProfile profile,
        IWpfBrowserWindowHost initialHost,
        string initialUrl,
        Func<Guid, string?, Task<IWpfBrowserWindowHost>> createTabHostCallback,
        Action<Guid, IBrowserWindowHost> closeTabHostCallback,
        Func<BrowserProfile, Task> openNewWindowCallback,
        Func<Guid, string, Task> onUrlVisitedCallback,
        Action<BrowserWorkspaceView> onAllTabsClosedCallback,
        Action? onBackToDashboardCallback = null,
        Guid? workspaceGroupId = null,
        string? workspaceGroupName = null)
    {
        InitializeComponent();

        _profile = profile;
        WorkspaceGroupId = workspaceGroupId;
        WorkspaceGroupName = workspaceGroupName;
        _createTabHostCallback = createTabHostCallback;
        _closeTabHostCallback = closeTabHostCallback;
        _openNewWindowCallback = openNewWindowCallback;
        _onUrlVisitedCallback = onUrlVisitedCallback;
        _onAllTabsClosedCallback = onAllTabsClosedCallback;
        _onBackToDashboardCallback = onBackToDashboardCallback;

        ProfileNameText.Text = string.IsNullOrWhiteSpace(workspaceGroupName)
            ? profile.Name
            : $"{workspaceGroupName}  •  {profile.Name}";
        ApplyBadgeColor(profile.ColorHex);
        BackToDashboardButton.Visibility = onBackToDashboardCallback is not null ? Visibility.Visible : Visibility.Collapsed;
        TabStrip.ItemsSource = _tabs;

        AttachTab(initialHost, initialUrl);
    }

    public Guid ProfileId => _profile.ProfileId;

    public string ProfileName => _profile.Name;

    public string ProfileColorHex => string.IsNullOrWhiteSpace(_profile.ColorHex) ? "#3B82F6" : _profile.ColorHex;

    public Guid? WorkspaceGroupId { get; }

    public string? WorkspaceGroupName { get; }

    public Guid WindowId { get; } = Guid.NewGuid();

    public event Action<string>? ActiveTitleChanged;

    public IReadOnlyList<TabSnapshotInput> CaptureOpenTabs()
        => _tabs.Select(t => new TabSnapshotInput(t.Address, t.Title)).ToList();

    public Task AddRestoredTabAsync(string url) => OpenTabAsync(url);

    /// <summary>
    /// Focuses an existing tab matching the target URL's host if one is already open,
    /// otherwise opens a new tab directly to <paramref name="targetUrl"/> (D10).
    /// </summary>
    public async Task FocusOrOpenUrlAsync(string targetUrl)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return;
        }

        var targetHost = ExtractNormalizedHost(targetUrl);
        if (!string.IsNullOrEmpty(targetHost))
        {
            var existing = _tabs.FirstOrDefault(t =>
                string.Equals(ExtractNormalizedHost(t.Address), targetHost, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                TabStrip.SelectedItem = existing;
                return;
            }
        }

        await OpenTabAsync(targetUrl);
    }

    private static string? ExtractNormalizedHost(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(uri.Host))
        {
            return null;
        }

        return uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase)
            ? uri.Host[4..]
            : uri.Host;
    }

    public void DisposeAllTabs()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (var tab in _tabs.ToList())
        {
            _closeTabHostCallback(_profile.ProfileId, tab.Host);
        }

        _tabs.Clear();
        BrowserContainer.Content = null;
    }

    private void AttachTab(IWpfBrowserWindowHost host, string initialUrl)
    {
        var tab = new BrowserTabItemViewModel(host, initialUrl);

        host.AddressChanged += address => OnTabAddressChanged(tab, address);
        host.TitleChanged += title => Dispatcher.BeginInvoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            tab.Title = title;
            if (_activeTab == tab)
            {
                ActiveTitleChanged?.Invoke($"{tab.Title} — {_profile.Name} | Switchly");
            }
        });
        host.LoadingStateChanged += isLoading => Dispatcher.BeginInvoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            tab.IsLoading = isLoading;
            if (_activeTab == tab)
            {
                UpdateNavigationButtons();
            }
        });
        host.NewTabRequested += url => Dispatcher.BeginInvoke(async () =>
        {
            if (!_disposed)
            {
                await OpenTabAsync(url);
            }
        });
        host.DownloadUpdated += evt => _ = RecordDownloadUpdateAsync(evt);
        host.RendererCrashed += reason => Dispatcher.BeginInvoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            if (_activeTab == tab)
            {
                CrashRecoveryMessageText.Text = $"{reason} Profil lain tetap aman. Klik Muat Ulang Tab untuk memulihkan.";
                CrashRecoveryBanner.Visibility = Visibility.Visible;
            }
        });
        host.ShortcutPressed += cmd => Dispatcher.BeginInvoke(async () =>
        {
            if (!_disposed)
            {
                await ExecuteShortcutAsync(cmd);
            }
        });

        _tabs.Add(tab);
        TabStrip.SelectedItem = tab;
    }

    private async Task OpenTabAsync(string? url = null)
    {
        try
        {
            var resolvedUrl = string.IsNullOrWhiteSpace(url) ? "about:blank" : url;
            var host = await _createTabHostCallback(_profile.ProfileId, resolvedUrl);
            AttachTab(host, resolvedUrl);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal membuka tab baru: {ex.Message}", "Switchly", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseTab(BrowserTabItemViewModel tab)
    {
        var index = _tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(tab.Address) && !string.Equals(tab.Address, "about:blank", StringComparison.OrdinalIgnoreCase))
        {
            _recentlyClosedUrls.Push(tab.Address);
        }

        _tabs.RemoveAt(index);
        _closeTabHostCallback(_profile.ProfileId, tab.Host);

        if (_tabs.Count == 0)
        {
            _onAllTabsClosedCallback(this);
            return;
        }

        var nextIndex = Math.Min(index, _tabs.Count - 1);
        TabStrip.SelectedItem = _tabs[nextIndex];
    }

    private void OnTabSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TabStrip.SelectedItem is not BrowserTabItemViewModel selected)
        {
            return;
        }

        _activeTab = selected;
        BrowserContainer.Content = selected.Host.Control;
        AddressBox.Text = selected.Address;
        CrashRecoveryBanner.Visibility = Visibility.Collapsed;
        ActiveTitleChanged?.Invoke($"{selected.Title} — {_profile.Name} | Switchly");
        UpdateNavigationButtons();
    }

    private void OnTabAddressChanged(BrowserTabItemViewModel tab, string address)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            tab.Address = address;
            if (_activeTab == tab)
            {
                AddressBox.Text = address;
                UpdateNavigationButtons();
            }
        });

        _ = RecordVisitAsync(address, tab.Title);
    }

    private async Task RecordVisitAsync(string address, string title)
    {
        if (string.IsNullOrWhiteSpace(address) ||
            address.StartsWith("about:", StringComparison.OrdinalIgnoreCase) ||
            address.StartsWith("chrome:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var history = scope.ServiceProvider.GetRequiredService<IHistoryService>();
            await history.RecordVisitAsync(_profile.ProfileId, address, title);
        }
        catch
        {
        }
    }

    private void UpdateNavigationButtons()
    {
        BackButton.IsEnabled = _activeTab?.Host.CanGoBack ?? false;
        ForwardButton.IsEnabled = _activeTab?.Host.CanGoForward ?? false;

        var isLoading = _activeTab?.IsLoading ?? false;
        LoadingProgressBar.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        var iconKey = isLoading ? "IconClose" : "IconRefresh";
        if (System.Windows.Application.Current?.TryFindResource(iconKey) is Geometry geometry)
        {
            ReloadIconPath.Data = geometry;
        }
    }

    private void ApplyBadgeColor(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return;
        }

        try
        {
            if (ColorConverter.ConvertFromString(colorHex) is Color color)
            {
                ProfileBadge.Background = new SolidColorBrush(color);
            }
        }
        catch
        {
        }
    }

    private void OnBackToDashboardClick(object sender, RoutedEventArgs e)
        => _onBackToDashboardCallback?.Invoke();

    private void OnBackClick(object sender, RoutedEventArgs e) => _activeTab?.Host.GoBack();

    private void OnForwardClick(object sender, RoutedEventArgs e) => _activeTab?.Host.GoForward();

    private void OnReloadClick(object sender, RoutedEventArgs e)
    {
        CrashRecoveryBanner.Visibility = Visibility.Collapsed;
        if (_activeTab is null)
        {
            return;
        }

        if (_activeTab.IsLoading)
        {
            _activeTab.Host.Stop();
        }
        else
        {
            _activeTab.Host.Reload();
        }
    }

    private async void OnNewTabClick(object sender, RoutedEventArgs e) => await OpenTabAsync();

    private void OnCloseTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BrowserTabItemViewModel tab })
        {
            CloseTab(tab);
        }
    }

    private void OnNavigateClick(object sender, RoutedEventArgs e) => NavigateToAddressBox();

    private void OnAddressBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateToAddressBox();
        }
    }

    private async void OnNewWindowClick(object sender, RoutedEventArgs e)
        => await _openNewWindowCallback(_profile);

    private async void OnBookmarkMenuClick(object sender, RoutedEventArgs e)
    {
        if (_activeTab is null || string.IsNullOrWhiteSpace(_activeTab.Address))
        {
            OpenLibraryDialog(ProfileLibraryTab.Bookmarks);
            return;
        }

        var address = _activeTab.Address;
        if (address.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            OpenLibraryDialog(ProfileLibraryTab.Bookmarks);
            return;
        }

        var choice = MessageBox.Show(
            $"Simpan '{_activeTab.Title}' ke Bookmark profil '{_profile.Name}'?\n\nPilih 'No' untuk membuka Pengelola Bookmark.",
            "Bookmark Profil",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        if (choice == MessageBoxResult.Yes)
        {
            try
            {
                using var scope = App.Services.CreateScope();
                var bookmarks = scope.ServiceProvider.GetRequiredService<IBookmarkService>();
                await bookmarks.AddBookmarkAsync(_profile.ProfileId, _activeTab.Title, address);
                MessageBox.Show($"Bookmark disimpan untuk profil '{_profile.Name}'.", "Switchly", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Gagal menyimpan bookmark: {ex.Message}", "Switchly", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
        else if (choice == MessageBoxResult.No)
        {
            OpenLibraryDialog(ProfileLibraryTab.Bookmarks);
        }
    }

    private void OnShowHistoryClick(object sender, RoutedEventArgs e)
        => OpenLibraryDialog(ProfileLibraryTab.History);

    private void OnShowDownloadsClick(object sender, RoutedEventArgs e)
        => OpenLibraryDialog(ProfileLibraryTab.Downloads);

    private void OpenLibraryDialog(ProfileLibraryTab initialTab)
    {
        var owner = Window.GetWindow(this);
        var dialog = new ProfileLibraryDialog(
            _profile.ProfileId,
            _profile.Name,
            initialTab,
            url => _activeTab?.Host.Navigate(url))
        {
            Owner = owner,
        };
        dialog.ShowDialog();
    }

    private async void OnAutofillPasswordClick(object sender, RoutedEventArgs e)
    {
        if (_activeTab is null || string.IsNullOrWhiteSpace(_activeTab.Address))
        {
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var vault = scope.ServiceProvider.GetRequiredService<IPasswordVaultService>();
            var matches = await vault.FindMatchesForUrlAsync(_profile.ProfileId, _activeTab.Address);

            if (matches.Count == 0)
            {
                MessageBox.Show(
                    "Tidak ada kredensial tersimpan di Password Vault profil ini untuk origin halaman saat ini.\n\nTambahkan atau impor CSV password lewat menu Keamanan & Backup → Password Vault.",
                    "Autofill Password",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            VaultCredentialMatch? selected = matches[0];
            if (matches.Count > 1)
            {
                var picker = new CredentialPickerDialog(matches) { Owner = Window.GetWindow(this) };
                if (picker.ShowDialog() != true)
                {
                    return;
                }

                selected = picker.SelectedMatch;
            }

            if (selected is null)
            {
                return;
            }

            var filled = await _activeTab.Host.FillLoginFormAsync(selected.Username, selected.Password);
            if (!filled)
            {
                MessageBox.Show(
                    "Kolom login/password yang terlihat tidak ditemukan di halaman saat ini. Pastikan halaman sudah memuat form login.",
                    "Autofill Password",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal mengisi kredensial: {ex.Message}", "Autofill Password", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static async Task RecordDownloadUpdateAsync(DownloadUpdateEvent evt)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var downloads = scope.ServiceProvider.GetRequiredService<IDownloadService>();
            await downloads.RecordOrUpdateAsync(evt);
        }
        catch
        {
        }
    }

    private void OnRecoverCrashedTabClick(object sender, RoutedEventArgs e)
    {
        CrashRecoveryBanner.Visibility = Visibility.Collapsed;
        _activeTab?.Host.Reload();
    }

    private async Task ExecuteShortcutAsync(BrowserShortcutCommand command)
    {
        switch (command)
        {
            case BrowserShortcutCommand.NewTab:
                await OpenTabAsync();
                break;
            case BrowserShortcutCommand.CloseTab when _activeTab is not null:
                CloseTab(_activeTab);
                break;
            case BrowserShortcutCommand.ReopenClosedTab when _recentlyClosedUrls.Count > 0:
                await OpenTabAsync(_recentlyClosedUrls.Pop());
                break;
            case BrowserShortcutCommand.FocusAddressBar:
                AddressBox.Focus();
                AddressBox.SelectAll();
                break;
            case BrowserShortcutCommand.Reload:
                CrashRecoveryBanner.Visibility = Visibility.Collapsed;
                _activeTab?.Host.Reload();
                break;
            case BrowserShortcutCommand.NextTab when _tabs.Count > 1 && _activeTab is not null:
                var next = (_tabs.IndexOf(_activeTab) + 1) % _tabs.Count;
                TabStrip.SelectedItem = _tabs[next];
                break;
            case BrowserShortcutCommand.ZoomIn:
                _activeTab?.Host.ZoomIn();
                break;
            case BrowserShortcutCommand.ZoomOut:
                _activeTab?.Host.ZoomOut();
                break;
            case BrowserShortcutCommand.ResetZoom:
                _activeTab?.Host.ResetZoom();
                break;
            case BrowserShortcutCommand.QuickSearch:
                if (Window.GetWindow(this) is MainWindow mainWindow)
                {
                    mainWindow.FocusQuickSearch();
                }
                break;
        }
    }

    private async void OnWorkspacePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F5)
        {
            e.Handled = true;
            await ExecuteShortcutAsync(BrowserShortcutCommand.Reload);
            return;
        }

        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.T)
        {
            if (_recentlyClosedUrls.Count > 0)
            {
                e.Handled = true;
                await ExecuteShortcutAsync(BrowserShortcutCommand.ReopenClosedTab);
            }

            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        BrowserShortcutCommand? cmd = e.Key switch
        {
            Key.T => BrowserShortcutCommand.NewTab,
            Key.W when _activeTab is not null => BrowserShortcutCommand.CloseTab,
            Key.L => BrowserShortcutCommand.FocusAddressBar,
            Key.R => BrowserShortcutCommand.Reload,
            Key.Tab when _tabs.Count > 1 && _activeTab is not null => BrowserShortcutCommand.NextTab,
            Key.OemPlus or Key.Add => BrowserShortcutCommand.ZoomIn,
            Key.OemMinus or Key.Subtract => BrowserShortcutCommand.ZoomOut,
            Key.D0 or Key.NumPad0 => BrowserShortcutCommand.ResetZoom,
            _ => null,
        };

        if (cmd is not null)
        {
            e.Handled = true;
            await ExecuteShortcutAsync(cmd.Value);
        }
    }

    private void NavigateToAddressBox()
    {
        if (_activeTab is null)
        {
            return;
        }

        var resolvedUrl = BrowserUrlPolicy.ResolveAddressBarInput(AddressBox.Text);
        if (resolvedUrl is null)
        {
            MessageBox.Show(
                "Skema URL ini diblokir demi keamanan. Hanya http://, https://, dan about:blank yang diizinkan.",
                "Switchly",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            AddressBox.Text = _activeTab.Address;
            return;
        }

        _activeTab.Host.Navigate(resolvedUrl);
    }
}
