using CefSharp;
using Switchly.Domain.Browser;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Routes new-tab / new-window popups back to the managed BrowserWindow in the same
/// profile context (TEST-021), while allowing OAuth-style NewPopup dialogs to inherit
/// the same RequestContext natively.
/// </summary>
internal sealed class ProfileLifeSpanHandler : ILifeSpanHandler
{
    private readonly Action<string> _onNewTabRequested;

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

        if (targetDisposition is WindowOpenDisposition.NewForegroundTab
            or WindowOpenDisposition.NewBackgroundTab
            or WindowOpenDisposition.NewWindow)
        {
            if (BrowserUrlPolicy.IsAllowedNavigationUrl(targetUrl))
            {
                _onNewTabRequested(targetUrl);
            }

            return true;
        }

        // Allow small OAuth/sign-in modal popups only when targetUrl uses a safe scheme (http/https/about:blank/empty).
        return !BrowserUrlPolicy.IsAllowedPopupUrl(targetUrl);
    }

    public void OnAfterCreated(IWebBrowser chromiumWebBrowser, IBrowser browser)
    {
    }

    public bool DoClose(IWebBrowser chromiumWebBrowser, IBrowser browser) => false;

    public void OnBeforeClose(IWebBrowser chromiumWebBrowser, IBrowser browser)
    {
    }
}
