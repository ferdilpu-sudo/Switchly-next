using System.Windows;
using CefSharp;
using CefSharp.Wpf;
using Switchly.Application.Browser;
using Switchly.BrowserHost.CefCallbacks;

namespace Switchly.BrowserHost.BrowserAdapters;

/// <summary>WPF ChromiumWebBrowser tab host bound to a single isolated CefProfileContext.</summary>
internal sealed class CefWpfWindowHost : IWpfBrowserWindowHost
{
    private readonly ChromiumWebBrowser _browser;
    private bool _disposed;

    public CefWpfWindowHost(CefProfileContext profileContext, string initialUrl)
    {
        ArgumentNullException.ThrowIfNull(profileContext);

        _browser = new ChromiumWebBrowser(initialUrl)
        {
            RequestContext = profileContext.RequestContext,
            LifeSpanHandler = new ProfileLifeSpanHandler(url => NewTabRequested?.Invoke(url)),
            DownloadHandler = new ProfileDownloadHandler(profileContext.ProfileId, evt => DownloadUpdated?.Invoke(evt)),
        };

        _browser.AddressChanged += OnBrowserAddressChanged;
        _browser.TitleChanged += OnBrowserTitleChanged;
        _browser.LoadingStateChanged += OnBrowserLoadingStateChanged;
    }

    public FrameworkElement Control => _browser;

    public bool CanGoBack => _browser.CanGoBack;

    public bool CanGoForward => _browser.CanGoForward;

    public bool IsLoading => _browser.IsLoading;

    public string? Address => _browser.Address;

    public string? Title => _browser.Title;

    public event Action<string>? AddressChanged;

    public event Action<string>? TitleChanged;

    public event Action<bool>? LoadingStateChanged;

    public event Action<string>? NewTabRequested;

    public event Action<DownloadUpdateEvent>? DownloadUpdated;

    public void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        _browser.Load(url);
    }

    public void GoBack()
    {
        if (_browser.CanGoBack)
        {
            _browser.Back();
        }
    }

    public void GoForward()
    {
        if (_browser.CanGoForward)
        {
            _browser.Forward();
        }
    }

    public void Reload() => _browser.Reload();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _browser.AddressChanged -= OnBrowserAddressChanged;
        _browser.TitleChanged -= OnBrowserTitleChanged;
        _browser.LoadingStateChanged -= OnBrowserLoadingStateChanged;
        _browser.Dispose();
    }

    private void OnBrowserAddressChanged(object sender, DependencyPropertyChangedEventArgs e)
        => AddressChanged?.Invoke((e.NewValue as string) ?? string.Empty);

    private void OnBrowserTitleChanged(object sender, DependencyPropertyChangedEventArgs e)
        => TitleChanged?.Invoke((e.NewValue as string) ?? string.Empty);

    private void OnBrowserLoadingStateChanged(object? sender, LoadingStateChangedEventArgs e)
        => LoadingStateChanged?.Invoke(e.IsLoading);
}
