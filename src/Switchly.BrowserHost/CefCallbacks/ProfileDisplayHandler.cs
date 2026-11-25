using CefSharp;
using CefSharp.Handler;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Forwards CEF link hover status messages to the WPF shell for the modern bottom-left URL preview bubble.
/// </summary>
internal sealed class ProfileDisplayHandler : DisplayHandler
{
    private readonly Action<string> _onStatusMessageChanged;

    public ProfileDisplayHandler(Action<string> onStatusMessageChanged)
    {
        _onStatusMessageChanged = onStatusMessageChanged ?? throw new ArgumentNullException(nameof(onStatusMessageChanged));
    }

    protected override void OnStatusMessage(IWebBrowser chromiumWebBrowser, StatusMessageEventArgs statusMessageArgs)
    {
        _onStatusMessageChanged(statusMessageArgs.Value ?? string.Empty);
    }
}
