using CefSharp;
using CefSharp.Handler;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Forwards CEF link hover status messages and HTML5 fullscreen mode transitions to the WPF shell.
/// </summary>
internal sealed class ProfileDisplayHandler : DisplayHandler
{
    private readonly Action<string> _onStatusMessageChanged;
    private readonly Action<bool> _onFullscreenModeChanged;

    public ProfileDisplayHandler(Action<string> onStatusMessageChanged, Action<bool> onFullscreenModeChanged)
    {
        _onStatusMessageChanged = onStatusMessageChanged ?? throw new ArgumentNullException(nameof(onStatusMessageChanged));
        _onFullscreenModeChanged = onFullscreenModeChanged ?? throw new ArgumentNullException(nameof(onFullscreenModeChanged));
    }

    protected override void OnStatusMessage(IWebBrowser chromiumWebBrowser, StatusMessageEventArgs statusMessageArgs)
    {
        _onStatusMessageChanged(statusMessageArgs.Value ?? string.Empty);
    }

    protected override void OnFullscreenModeChange(IWebBrowser chromiumWebBrowser, IBrowser browser, bool fullscreen)
    {
        _onFullscreenModeChanged(fullscreen);
    }
}
