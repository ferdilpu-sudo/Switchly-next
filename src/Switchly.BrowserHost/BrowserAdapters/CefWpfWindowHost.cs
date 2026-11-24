using System.Windows;
using CefSharp;
using CefSharp.Wpf;

namespace Switchly.BrowserHost.BrowserAdapters;

/// <summary>WPF ChromiumWebBrowser host bound to a single isolated CefProfileContext.</summary>
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
        };

        _browser.AddressChanged += OnBrowserAddressChanged;
    }

    public FrameworkElement Control => _browser;

    public string? Address => _browser.Address;

    public event Action<string>? AddressChanged;

    public void Navigate(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        _browser.Load(url);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _browser.AddressChanged -= OnBrowserAddressChanged;
        _browser.Dispose();
    }

    private void OnBrowserAddressChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        AddressChanged?.Invoke((e.NewValue as string) ?? string.Empty);
    }
}
