using CefSharp;
using Switchly.Domain.Browser;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Routes new-tab / new-window popups back to the managed BrowserWindow in the same
/// profile context (TEST-021), while tracking OAuth-style NewPopup dialogs so they are
/// deterministically closed when the profile window host is disposed (CEF-02).
/// </summary>
internal sealed class ProfileLifeSpanHandler : ILifeSpanHandler
{
    private readonly Action<string> _onNewTabRequested;
    private readonly object _popupGate = new();
    private readonly HashSet<IBrowser> _childPopups = [];

    public ProfileLifeSpanHandler(Action<string> onNewTabRequested)
    {
        _onNewTabRequested = onNewTabRequested ?? throw new ArgumentNullException(nameof(onNewTabRequested));
    }

    public bool OnBeforePopup(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        string targetUrl,
        string targetFrameName,
        WindowOpenDisposition targetDisposition,
        bool userGesture,
        IPopupFeatures popupFeatures,
        IWindowInfo windowInfo,
        IBrowserSettings browserSettings,
        ref bool noJavascriptAccess,
        out IWebBrowser? newBrowser)
    {
        newBrowser = null;

        if (targetDisposition == WindowOpenDisposition.OffTheRecord)
        {
            return true;
        }

        if (targetDisposition is WindowOpenDisposition.NewForegroundTab
            or WindowOpenDisposition.NewBackgroundTab
            or WindowOpenDisposition.NewWindow
            or WindowOpenDisposition.SingletonTab
            or WindowOpenDisposition.SwitchToTab)
        {
            if (BrowserUrlPolicy.IsAllowedNavigationUrl(targetUrl))
            {
                _onNewTabRequested(targetUrl);
            }

            return true;
        }

        // Allow small OAuth/sign-in modal popups only for NewPopup when targetUrl uses a safe scheme.
        if (targetDisposition == WindowOpenDisposition.NewPopup && BrowserUrlPolicy.IsAllowedPopupUrl(targetUrl))
        {
            return false;
        }

        if (BrowserUrlPolicy.IsAllowedNavigationUrl(targetUrl))
        {
            _onNewTabRequested(targetUrl);
        }

        return true;
    }

    public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
    {
        if (browser is { IsPopup: true })
        {
            lock (_popupGate)
            {
                _childPopups.Add(browser);
            }
        }
    }

    public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) => false;

    public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
    {
        if (browser is { IsPopup: true })
        {
            lock (_popupGate)
            {
                _childPopups.Remove(browser);
            }
        }
    }

    public void CloseAllPopups()
    {
        List<IBrowser> popups;
        lock (_popupGate)
        {
            popups = [.. _childPopups];
            _childPopups.Clear();
        }

        foreach (var popup in popups)
        {
            try
            {
                if (!popup.IsDisposed)
                {
                    popup.GetHost()?.CloseBrowser(forceClose: true);
                }
            }
            catch
            {
            }
        }
    }
}
