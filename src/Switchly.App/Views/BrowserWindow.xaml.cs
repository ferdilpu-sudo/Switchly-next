using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Switchly.Application.Browser;
using Switchly.BrowserHost.BrowserAdapters;
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>Isolated browser window bound permanently to a single ProfileId (UI-003, REQ-001, REQ-005).</summary>
public partial class BrowserWindow : Window
{
    private readonly BrowserProfile _profile;
    private readonly IWpfBrowserWindowHost _browserHost;
    private readonly Func<BrowserProfile, Task> _openNewWindowCallback;
    private readonly Func<Guid, string, Task> _onUrlVisitedCallback;
    private readonly Action<Guid, IBrowserWindowHost, BrowserWindow> _onWindowClosingCallback;
    private bool _closedNotified;

    public BrowserWindow(
        BrowserProfile profile,
        IWpfBrowserWindowHost browserHost,
        Func<BrowserProfile, Task> openNewWindowCallback,
        Func<Guid, string, Task> onUrlVisitedCallback,
        Action<Guid, IBrowserWindowHost, BrowserWindow> onWindowClosingCallback)
    {
        InitializeComponent();

        _profile = profile;
        _browserHost = browserHost;
        _openNewWindowCallback = openNewWindowCallback;
        _onUrlVisitedCallback = onUrlVisitedCallback;
        _onWindowClosingCallback = onWindowClosingCallback;

        Title = $"Switchly Next — {profile.Name}";
        ProfileNameText.Text = profile.Name;
        ApplyBadgeColor(profile.ColorHex);
        AddressBox.Text = browserHost.Address ?? profile.StartupUrl ?? "about:blank";
        BrowserContainer.Content = browserHost.Control;

        _browserHost.AddressChanged += OnBrowserAddressChanged;
        Closed += OnBrowserWindowClosed;
    }

    public Guid ProfileId => _profile.ProfileId;

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

    private void NavigateToAddressBox()
    {
        var url = AddressBox.Text.Trim();
        if (url.Length == 0)
        {
            return;
        }

        if (!url.Contains("://", StringComparison.Ordinal))
        {
            url = "https://" + url;
        }

        _browserHost.Navigate(url);
    }

    private void OnBrowserAddressChanged(string address)
    {
        Dispatcher.Invoke(() => AddressBox.Text = address);

        if (address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            _ = _onUrlVisitedCallback(_profile.ProfileId, address);
        }
    }

    private void OnBrowserWindowClosed(object? sender, EventArgs e)
    {
        if (_closedNotified)
        {
            return;
        }

        _closedNotified = true;
        _browserHost.AddressChanged -= OnBrowserAddressChanged;
        _onWindowClosingCallback(_profile.ProfileId, _browserHost, this);
    }
}
