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
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>
/// Multi-tab isolated browser window bound permanently to one ProfileId (UI-003, REQ-001, REQ-005, REQ-006).
/// All tabs inside this window share the profile's isolated RequestContext.
/// </summary>
public partial class BrowserWindow : Window
{
    private readonly BrowserProfile _profile;
    private readonly Func<Guid, string?, Task<IWpfBrowserWindowHost>> _createTabHostCallback;
    private readonly Action<Guid, IBrowserWindowHost> _closeTabHostCallback;
    private readonly Func<BrowserProfile, Task> _openNewWindowCallback;
    private readonly Func<Guid, string, Task> _onUrlVisitedCallback;
    private readonly Action<Guid, BrowserWindow> _onWindowClosedCallback;
    private readonly ObservableCollection<BrowserTabItemViewModel> _tabs = [];
    private readonly Stack<string> _recentlyClosedUrls = new();
    private BrowserTabItemViewModel? _activeTab;
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

        _profile = profile;
        _createTabHostCallback = createTabHostCallback;
        _closeTabHostCallback = closeTabHostCallback;
        _openNewWindowCallback = openNewWindowCallback;
        _onUrlVisitedCallback = onUrlVisitedCallback;
        _onWindowClosedCallback = onWindowClosedCallback;

        Title = $"Switchly Next — {profile.Name}";
        ProfileNameText.Text = profile.Name;
        ApplyBadgeColor(profile.ColorHex);
        TabStrip.ItemsSource = _tabs;

        AttachTab(initialHost, initialUrl);
        Closed += OnBrowserWindowClosed;
    }

    public Guid ProfileId => _profile.ProfileId;

    public Guid WindowId { get; } = Guid.NewGuid();

    public IReadOnlyList<TabSnapshotInput> CaptureOpenTabs()
        => _tabs.Select(t => new TabSnapshotInput(t.Address, t.Title)).ToList();

    public Task AddRestoredTabAsync(string url) => OpenTabAsync(url);

    private void AttachTab(IWpfBrowserWindowHost host, string initialUrl)
    {
        var tab = new BrowserTabItemViewModel(host, initialUrl);

        host.AddressChanged += address => OnTabAddressChanged(tab, address);
        host.TitleChanged += title => Dispatcher.Invoke(() =>
        {
            tab.Title = title;
            if (_activeTab == tab)
            {
                Title = $"{tab.Title} — {_profile.Name} | Switchly Next";
            }
        });
        host.LoadingStateChanged += isLoading => Dispatcher.Invoke(() =>
        {
            tab.IsLoading = isLoading;
            if (_activeTab == tab)
            {
                UpdateNavigationButtons();
            }
        });
        host.NewTabRequested += url => Dispatcher.InvokeAsync(() => OpenTabAsync(url));
        host.DownloadUpdated += evt => _ = RecordDownloadUpdateAsync(evt);
        host.RendererCrashed += reason => Dispatcher.Invoke(() =>
        {
            if (_activeTab == tab)
            {
                CrashRecoveryMessageText.Text = $"{reason} Profil lain tetap aman. Klik Muat Ulang Tab untuk memulihkan.";
                CrashRecoveryBanner.Visibility = Visibility.Visible;
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
            MessageBox.Show($"Gagal membuka tab baru: {ex.Message}", "Switchly Next", MessageBoxButton.OK, MessageBoxImage.Error);
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
            Close();
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
        BrowserContainer.Content = selected.Control;
        AddressBox.Text = selected.Address;
        Title = $"{selected.Title} — {_profile.Name} | Switchly Next";
        UpdateNavigationButtons();
    }

    private void OnTabAddressChanged(BrowserTabItemViewModel tab, string address)
    {
        Dispatcher.Invoke(() =>
        {
            tab.Address = address;
            if (_activeTab == tab)
            {
                AddressBox.Text = address;
                UpdateNavigationButtons();
            }
        });

        if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _ = _onUrlVisitedCallback(_profile.ProfileId, address);
        }
    }

    private void UpdateNavigationButtons()
    {
        if (_activeTab is null)
        {
            BackButton.IsEnabled = false;
            ForwardButton.IsEnabled = false;
            return;
        }

        BackButton.IsEnabled = _activeTab.Host.CanGoBack;
        ForwardButton.IsEnabled = _activeTab.Host.CanGoForward;
    }

    private void ApplyBadgeColor(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex))
        {
            return;
        }

        try
        {
            var color = (Color)ColorConverter.ConvertFromString(colorHex);
            ProfileBadge.Background = new SolidColorBrush(color);
        }
        catch
        {
            // Keep default badge color if conversion fails.
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

    private void OnBackClick(object sender, RoutedEventArgs e) => _activeTab?.Host.GoBack();

    private void OnForwardClick(object sender, RoutedEventArgs e) => _activeTab?.Host.GoForward();

    private void OnReloadClick(object sender, RoutedEventArgs e) => _activeTab?.Host.Reload();

    private void OnNavigateClick(object sender, RoutedEventArgs e) => NavigateToAddressBox();

    private void OnAddressBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            NavigateToAddressBox();
        }
    }

    private async void OnNewWindowClick(object sender, RoutedEventArgs e)
    {
        try
        {
            await _openNewWindowCallback(_profile);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal membuka jendela baru: {ex.Message}", "Switchly Next", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnBookmarkCurrentClick(object sender, RoutedEventArgs e)
    {
        if (_activeTab is null || string.IsNullOrWhiteSpace(_activeTab.Address))
        {
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var bookmarks = scope.ServiceProvider.GetRequiredService<IBookmarkService>();
            var saved = await bookmarks.AddBookmarkAsync(_profile.ProfileId, _activeTab.Title, _activeTab.Address);
            MessageBox.Show($"Bookmark disimpan untuk '{_profile.Name}':\n{saved.Title}", "Switchly Next", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal menyimpan bookmark: {ex.Message}", "Switchly Next", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void OnShowHistoryClick(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var history = scope.ServiceProvider.GetRequiredService<IHistoryService>();
            var entries = await history.ListAsync(_profile.ProfileId, limit: 15);
            var summary = entries.Count == 0
                ? "Belum ada riwayat untuk profil ini."
                : string.Join(Environment.NewLine, entries.Select(h => $"• {h.Url} ({h.VisitCount}x)"));
            MessageBox.Show(summary, $"Riwayat — {_profile.Name}", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal memuat riwayat: {ex.Message}", "Switchly Next", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void OnShowDownloadsClick(object sender, RoutedEventArgs e)
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var downloads = scope.ServiceProvider.GetRequiredService<IDownloadService>();
            var items = await downloads.ListByProfileAsync(_profile.ProfileId, limit: 15);
            var summary = items.Count == 0
                ? "Belum ada unduhan untuk profil ini."
                : string.Join(Environment.NewLine, items.Select(d => $"• {d.SuggestedFileName} [{d.State}]"));
            MessageBox.Show(summary, $"Unduhan — {_profile.Name}", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal memuat unduhan: {ex.Message}", "Switchly Next", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
                var picker = new CredentialPickerDialog(matches) { Owner = this };
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
            // Browser transfer remains authoritative; do not crash UI on metadata logging error.
        }
    }

    private void OnRecoverCrashedTabClick(object sender, RoutedEventArgs e)
    {
        CrashRecoveryBanner.Visibility = Visibility.Collapsed;
        _activeTab?.Host.Reload();
    }

    private async void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Keyboard.Modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.T)
        {
            if (_recentlyClosedUrls.Count > 0)
            {
                e.Handled = true;
                var restoredUrl = _recentlyClosedUrls.Pop();
                await OpenTabAsync(restoredUrl);
            }

            return;
        }

        if (Keyboard.Modifiers != ModifierKeys.Control)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.T:
                e.Handled = true;
                await OpenTabAsync();
                break;
            case Key.W when _activeTab is not null:
                e.Handled = true;
                CloseTab(_activeTab);
                break;
            case Key.L:
                e.Handled = true;
                AddressBox.Focus();
                AddressBox.SelectAll();
                break;
            case Key.Tab when _tabs.Count > 1 && _activeTab is not null:
                e.Handled = true;
                var next = (_tabs.IndexOf(_activeTab) + 1) % _tabs.Count;
                TabStrip.SelectedItem = _tabs[next];
                break;
        }
    }

    private void NavigateToAddressBox()
    {
        if (_activeTab is null)
        {
            return;
        }

        var url = AddressBox.Text.Trim();
        if (url.Length == 0)
        {
            return;
        }

        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = "https://" + url;
        }

        _activeTab.Host.Navigate(url);
    }

    private void OnBrowserWindowClosed(object? sender, EventArgs e)
    {
        if (_closedNotified)
        {
            return;
        }

        _closedNotified = true;
        _onWindowClosedCallback(_profile.ProfileId, this);
        foreach (var tab in _tabs.ToList())
        {
            _closeTabHostCallback(_profile.ProfileId, tab.Host);
        }

        _tabs.Clear();
    }
}
