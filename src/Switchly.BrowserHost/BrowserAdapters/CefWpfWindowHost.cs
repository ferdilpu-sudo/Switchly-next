using System.Text.Json;
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

    public async Task<bool> FillLoginFormAsync(string username, string password)
    {
        if (_disposed || !_browser.IsBrowserInitialized)
        {
            return false;
        }

        var userJson = JsonSerializer.Serialize(username ?? string.Empty);
        var passJson = JsonSerializer.Serialize(password ?? string.Empty);

        // One-shot IIFE using native HTMLInputElement value setter so React/Vue/Next.js/Angular
        // controlled inputs (ChatGPT, Google Flow, Dola, SeaArt) receive the updated value cleanly.
        var script = $$"""
            (function(u, p) {
                function isVisible(el) {
                    if (!el || el.disabled || el.readOnly) return false;
                    var style = window.getComputedStyle(el);
                    if (style.display === 'none' || style.visibility === 'hidden') return false;
                    var rect = el.getBoundingClientRect();
                    return rect.width > 0 && rect.height > 0;
                }
                function setNativeValue(el, val) {
                    var proto = window.HTMLInputElement && window.HTMLInputElement.prototype;
                    var desc = proto ? Object.getOwnPropertyDescriptor(proto, 'value') : null;
                    if (desc && desc.set) {
                        desc.set.call(el, val);
                    } else {
                        el.value = val;
                    }
                    el.dispatchEvent(new Event('input', { bubbles: true }));
                    el.dispatchEvent(new Event('change', { bubbles: true }));
                }

                var filled = false;
                var allInputs = Array.from(document.querySelectorAll('input')).filter(isVisible);
                var passInput = allInputs.find(function(i) { return (i.type || '').toLowerCase() === 'password'; });

                var userCandidates = allInputs.filter(function(i) {
                    var t = (i.type || 'text').toLowerCase();
                    if (t !== 'email' && t !== 'text' && t !== 'tel') return false;
                    var hint = ((i.name || '') + ' ' + (i.id || '') + ' ' + (i.autocomplete || '') + ' ' + (i.placeholder || '')).toLowerCase();
                    return t === 'email' || /user|email|login|account|identifier|phone/.test(hint) || !!passInput;
                });

                var userInput = null;
                if (passInput && passInput.form) {
                    userInput = userCandidates.find(function(i) { return i.form === passInput.form; }) || null;
                }
                if (!userInput && userCandidates.length > 0) {
                    userInput = userCandidates[0];
                }

                if (userInput && u) {
                    setNativeValue(userInput, u);
                    filled = true;
                }
                if (passInput && p) {
                    setNativeValue(passInput, p);
                    filled = true;
                }
                return filled;
            })({{userJson}}, {{passJson}});
            """;

        try
        {
            var response = await _browser.EvaluateScriptAsync(script, timeout: TimeSpan.FromSeconds(5));
            return response.Success && response.Result is bool ok && ok;
        }
        catch
        {
            return false;
        }
    }

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
