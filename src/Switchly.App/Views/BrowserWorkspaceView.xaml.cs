using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
    private readonly ObservableCollection<DownloadItemViewModel> _recentDownloads = [];
    private readonly Stack<string> _recentlyClosedUrls = new();
    private readonly DispatcherTimer _toastTimer;
    private BrowserTabItemViewModel? _activeTab;
    private bool _isFullscreen;
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
        // D14/D15: TopProfileStripBar in MainWindow already shows Profil, Aktivitas, and the active
        // "Group • Profile" chip when docked, so hide duplicate tab-strip BackToDashboardButton and ProfileBadge
        // in docked mode (keep ProfileBadge visible in standalone Pop-out BrowserWindow).
        BackToDashboardButton.Visibility = Visibility.Collapsed;
        ProfileBadge.Visibility = onBackToDashboardCallback is null ? Visibility.Visible : Visibility.Collapsed;
        TabStrip.ItemsSource = _tabs;
        DownloadFlyoutList.ItemsSource = _recentDownloads;

        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.2) };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            ToastBanner.Visibility = Visibility.Collapsed;
        };

        AttachTab(initialHost, initialUrl);
    }

    public Guid ProfileId => _profile.ProfileId;

    public string ProfileName => _profile.Name;

    public string ProfileColorHex => string.IsNullOrWhiteSpace(_profile.ColorHex) ? "#3B82F6" : _profile.ColorHex;

    public Guid? WorkspaceGroupId { get; }

    public string? WorkspaceGroupName { get; }

    public Guid WindowId { get; } = Guid.NewGuid();

    public event Action<string>? ActiveTitleChanged;

    public event Action<bool>? FullscreenModeChanged;

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

        if (_isFullscreen)
        {
            SetFullscreenMode(false);
        }

        _toastTimer.Stop();
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
        host.DownloadUpdated += evt => _ = HandleDownloadUpdateAsync(evt);
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
        host.StatusMessageChanged += message => Dispatcher.BeginInvoke(() =>
        {
            if (_disposed || _activeTab != tab)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(message))
            {
                HoverStatusBubble.Visibility = Visibility.Collapsed;
            }
            else
            {
                HoverStatusText.Text = message;
                HoverStatusBubble.Visibility = Visibility.Visible;
            }
        });
        host.ZoomPercentChanged += percent => Dispatcher.BeginInvoke(() =>
        {
            if (_disposed || _activeTab != tab)
            {
                return;
            }

            ZoomBadgeButton.Content = $"{percent}%";
            ZoomBadgeButton.Visibility = percent == 100 ? Visibility.Collapsed : Visibility.Visible;
        });
        host.FullscreenModeChanged += isFullscreen => Dispatcher.BeginInvoke(() =>
        {
            if (_disposed || _activeTab != tab)
            {
                return;
            }

            SetFullscreenMode(isFullscreen);
        });

        _tabs.Add(tab);
        TabStrip.SelectedItem = tab;
    }

    private void SetFullscreenMode(bool isFullscreen)
    {
        if (_isFullscreen == isFullscreen)
        {
            return;
        }

        _isFullscreen = isFullscreen;
        var chromeVisibility = isFullscreen ? Visibility.Collapsed : Visibility.Visible;
        TabStripBar.Visibility = chromeVisibility;
        NavigationToolbarBar.Visibility = chromeVisibility;
        if (isFullscreen)
        {
            FindBar.Visibility = Visibility.Collapsed;
            HoverStatusBubble.Visibility = Visibility.Collapsed;
            DownloadFlyoutPanel.Visibility = Visibility.Collapsed;
        }

        FullscreenModeChanged?.Invoke(isFullscreen);
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

        if (_isFullscreen && _activeTab != selected)
        {
            SetFullscreenMode(false);
        }

        _activeTab = selected;
        BrowserContainer.Content = selected.Host.Control;
        AddressBox.Text = selected.Address;
        UpdateSecurityIcon(selected.Address);
        HoverStatusBubble.Visibility = Visibility.Collapsed;
        DownloadFlyoutPanel.Visibility = Visibility.Collapsed;
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
                if (!AddressBox.IsKeyboardFocusWithin)
                {
                    AddressBox.Text = address;
                }

                UpdateSecurityIcon(address);
                UpdateNavigationButtons();
            }
        });

        _ = RecordVisitAsync(address, tab.Title);
        if (!string.IsNullOrWhiteSpace(address) &&
            (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
             address.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
        {
            _ = _onUrlVisitedCallback(_profile.ProfileId, address);
        }
    }

    private void UpdateSecurityIcon(string? address)
    {
        var isHttps = !string.IsNullOrWhiteSpace(address)
            && address.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

        var iconKey = isHttps ? "IconLock" : "IconGlobe";
        if (System.Windows.Application.Current?.TryFindResource(iconKey) is Geometry geometry)
        {
            SecurityIconPath.Data = geometry;
        }

        var brushKey = isHttps ? "SuccessTextBrush" : "TextMutedBrush";
        SecurityIconPath.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, brushKey);

        SecurityIconPath.ToolTip = isHttps
            ? "Koneksi aman (HTTPS)"
            : "Halaman lokal / non-HTTPS";
    }

    private void ShowToast(string message)
    {
        ToastText.Text = message;
        ToastBanner.Visibility = Visibility.Visible;
        _toastTimer.Stop();
        _toastTimer.Start();
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
        var isInitialBlankLoad = isLoading &&
            (string.IsNullOrWhiteSpace(_activeTab?.Title) ||
             string.Equals(_activeTab?.Title, "Tab Baru", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(_activeTab?.Title, "about:blank", StringComparison.OrdinalIgnoreCase));
        InitialLoadingOverlay.Visibility = isInitialBlankLoad ? Visibility.Visible : Visibility.Collapsed;
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

    private void OnTabItemMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && sender is FrameworkElement { Tag: BrowserTabItemViewModel tab })
        {
            e.Handled = true;
            CloseTab(tab);
        }
    }

    private void OnCloseTabClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BrowserTabItemViewModel tab })
        {
            CloseTab(tab);
        }
    }

    private void OnTabReloadContextClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BrowserTabItemViewModel tab })
        {
            tab.Host.Reload();
        }
    }

    private async void OnTabDuplicateContextClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BrowserTabItemViewModel tab })
        {
            await OpenTabAsync(tab.Address);
        }
    }

    private void OnTabCopyUrlContextClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BrowserTabItemViewModel tab } && !string.IsNullOrWhiteSpace(tab.Address))
        {
            try
            {
                Clipboard.SetText(tab.Address);
                ShowToast("URL tab disalin ke clipboard");
            }
            catch
            {
            }
        }
    }

    private void OnCloseOtherTabsContextClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: BrowserTabItemViewModel keepTab })
        {
            var others = _tabs.Where(t => t != keepTab).ToList();
            foreach (var other in others)
            {
                CloseTab(other);
            }

            TabStrip.SelectedItem = keepTab;
        }
    }

    private async void OnReopenClosedTabContextClick(object sender, RoutedEventArgs e)
    {
        if (_recentlyClosedUrls.Count > 0)
        {
            await OpenTabAsync(_recentlyClosedUrls.Pop());
        }
        else
        {
            ShowToast("Tidak ada tab tertutup yang bisa dibuka kembali");
        }
    }

    private void OnAddressPasteAndGoClick(object sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard.ContainsText())
            {
                AddressBox.Text = Clipboard.GetText().Trim();
                NavigateToAddressBox();
            }
        }
        catch
        {
        }
    }

    private void OnNavigateClick(object sender, RoutedEventArgs e) => NavigateToAddressBox();

    private void OnAddressBoxGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        AddressBox.SelectAll();
    }

    private void OnAddressBoxPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!AddressBox.IsKeyboardFocusWithin)
        {
            e.Handled = true;
            AddressBox.Focus();
            AddressBox.SelectAll();
        }
    }

    private void OnAddressBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateToAddressBox();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            AddressBox.Text = _activeTab?.Address ?? string.Empty;
            Keyboard.ClearFocus();
            e.Handled = true;
        }
    }

    private async void OnNewWindowClick(object sender, RoutedEventArgs e)
        => await _openNewWindowCallback(_profile);

    private void OnBookmarkRightClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        OpenLibraryDialog(ProfileLibraryTab.Bookmarks);
    }

    private async void OnBookmarkMenuClick(object sender, RoutedEventArgs e)
    {
        if (_activeTab is null || string.IsNullOrWhiteSpace(_activeTab.Address)
            || _activeTab.Address.StartsWith("about:", StringComparison.OrdinalIgnoreCase))
        {
            OpenLibraryDialog(ProfileLibraryTab.Bookmarks);
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var bookmarks = scope.ServiceProvider.GetRequiredService<IBookmarkService>();
            await bookmarks.AddBookmarkAsync(_profile.ProfileId, _activeTab.Title, _activeTab.Address);
            ShowToast($"Bookmark disimpan di profil {_profile.Name} (klik kanan untuk kelola)");
        }
        catch (Exception ex)
        {
            ShowToast($"Gagal menyimpan bookmark: {ex.Message}");
        }
    }

    private void OnResetZoomClick(object sender, RoutedEventArgs e) => _activeTab?.Host.ResetZoom();

    private void OnFindToggleClick(object sender, RoutedEventArgs e)
    {
        if (FindBar.Visibility == Visibility.Visible)
        {
            CloseFindBar();
        }
        else
        {
            OpenFindBar();
        }
    }

    private void OpenFindBar()
    {
        FindBar.Visibility = Visibility.Visible;
        FindBox.Focus();
        FindBox.SelectAll();
    }

    private void CloseFindBar()
    {
        FindBar.Visibility = Visibility.Collapsed;
        _activeTab?.Host.StopFinding(clearSelection: true);
    }

    private void OnCloseFindClick(object sender, RoutedEventArgs e) => CloseFindBar();

    private void OnFindNextClick(object sender, RoutedEventArgs e)
        => _activeTab?.Host.Find(FindBox.Text, forward: true, matchCase: false, findNext: true);

    private void OnFindPreviousClick(object sender, RoutedEventArgs e)
        => _activeTab?.Host.Find(FindBox.Text, forward: false, matchCase: false, findNext: true);

    private void OnFindBoxTextChanged(object sender, TextChangedEventArgs e)
    {
        var query = FindBox.Text;
        if (string.IsNullOrEmpty(query))
        {
            _activeTab?.Host.StopFinding(clearSelection: true);
        }
        else
        {
            _activeTab?.Host.Find(query, forward: true, matchCase: false, findNext: false);
        }
    }

    private void OnFindBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            CloseFindBar();
        }
        else if (e.Key == Key.Enter)
        {
            e.Handled = true;
            var forward = (Keyboard.Modifiers & ModifierKeys.Shift) == 0;
            _activeTab?.Host.Find(FindBox.Text, forward: forward, matchCase: false, findNext: true);
        }
    }

    private void OnShowHistoryClick(object sender, RoutedEventArgs e)
        => OpenLibraryDialog(ProfileLibraryTab.History);

    private async void OnShowDownloadsClick(object sender, RoutedEventArgs e)
    {
        if (DownloadFlyoutPanel.Visibility == Visibility.Visible)
        {
            DownloadFlyoutPanel.Visibility = Visibility.Collapsed;
            return;
        }

        await LoadRecentDownloadsAsync();
        DownloadFlyoutPanel.Visibility = Visibility.Visible;
    }

    private void OnCloseDownloadFlyoutClick(object sender, RoutedEventArgs e)
        => DownloadFlyoutPanel.Visibility = Visibility.Collapsed;

    private void OnOpenFullDownloadsClick(object sender, RoutedEventArgs e)
    {
        DownloadFlyoutPanel.Visibility = Visibility.Collapsed;
        OpenLibraryDialog(ProfileLibraryTab.Downloads);
    }

    private void OnFlyoutOpenFileClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DownloadItemViewModel item })
        {
            DownloadItemViewModel.OpenDownloadedFile(item.TargetPath);
        }
    }

    private void OnFlyoutShowInFolderClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: DownloadItemViewModel item })
        {
            DownloadItemViewModel.ShowInExplorer(item.TargetPath);
        }
    }

    private async Task LoadRecentDownloadsAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var downloads = scope.ServiceProvider.GetRequiredService<IDownloadService>();
            var records = await downloads.ListByProfileAsync(_profile.ProfileId, limit: 8);

            _recentDownloads.Clear();
            foreach (var record in records)
            {
                _recentDownloads.Add(DownloadItemViewModel.FromRecord(record));
            }

            UpdateDownloadIndicators();
        }
        catch
        {
        }
    }

    private void UpdateDownloadIndicators()
    {
        DownloadFlyoutEmptyText.Visibility = _recentDownloads.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        DownloadActiveDot.Visibility = _recentDownloads.Any(d => d.IsInProgress) ? Visibility.Visible : Visibility.Collapsed;
    }

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

            var filled = await _activeTab.Host.FillLoginFormAsync(selected.Username, selected.Password, selected.Origin);
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

    private async Task HandleDownloadUpdateAsync(DownloadUpdateEvent evt)
    {
        await RecordDownloadUpdateAsync(evt);

        _ = Dispatcher.BeginInvoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            var existing = _recentDownloads.FirstOrDefault(d =>
                string.Equals(d.TargetPath, evt.TargetPath, StringComparison.OrdinalIgnoreCase));

            var wasCompleted = existing?.IsCompleted == true;
            if (existing is not null)
            {
                existing.ApplyUpdate(evt);
            }
            else
            {
                _recentDownloads.Insert(0, DownloadItemViewModel.FromEvent(evt));
                while (_recentDownloads.Count > 8)
                {
                    _recentDownloads.RemoveAt(_recentDownloads.Count - 1);
                }
            }

            UpdateDownloadIndicators();

            if (evt.State == Domain.Downloads.DownloadState.Completed && !wasCompleted)
            {
                ShowToast($"Unduhan selesai: {evt.SuggestedFileName}");
            }
        });
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
            case BrowserShortcutCommand.FindInPage:
                OpenFindBar();
                break;
            case BrowserShortcutCommand.QuickSearch:
                var targetMain = Window.GetWindow(this) as MainWindow
                    ?? System.Windows.Application.Current?.MainWindow as MainWindow;
                if (targetMain is not null)
                {
                    targetMain.Activate();
                    targetMain.OpenProfileSwitcher();
                }
                break;
        }
    }

    private async void OnWorkspacePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            if (_isFullscreen)
            {
                SetFullscreenMode(false);
                e.Handled = true;
                return;
            }
            if (DownloadFlyoutPanel.Visibility == Visibility.Visible)
            {
                DownloadFlyoutPanel.Visibility = Visibility.Collapsed;
                e.Handled = true;
                return;
            }

            if (FindBar.Visibility == Visibility.Visible)
            {
                CloseFindBar();
                e.Handled = true;
                return;
            }
        }

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
            Key.F => BrowserShortcutCommand.FindInPage,
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
