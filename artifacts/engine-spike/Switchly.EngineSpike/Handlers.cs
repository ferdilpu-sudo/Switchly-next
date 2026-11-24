using CefSharp;
using CefSharp.OffScreen;

namespace Switchly.EngineSpike;

/// <summary>Captures a download and saves it to a temp folder; signals completion.</summary>
internal sealed class DownloadCaptureHandler : CefSharp.Handler.DownloadHandler
{
    private readonly TaskCompletionSource<string> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<string> Done => _done.Task;

    protected override bool OnBeforeDownload(IWebBrowser chromiumWebBrowser, IBrowser browser,
        DownloadItem downloadItem, IBeforeDownloadCallback callback)
    {
        var folder = Path.Combine(Path.GetTempPath(), "switchly-engine-spike-downloads");
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, downloadItem.SuggestedFileName);
        if (!callback.IsDisposed)
        {
            callback.Continue(path, showDialog: false);
        }
        return true;
    }

    protected override void OnDownloadUpdated(IWebBrowser chromiumWebBrowser, IBrowser browser,
        DownloadItem downloadItem, IDownloadItemCallback callback)
    {
        if (downloadItem.IsComplete)
        {
            _done.TrySetResult(downloadItem.FullPath);
        }
        else if (downloadItem.IsCancelled)
        {
            _done.TrySetResult(string.Empty);
        }
    }
}

/// <summary>Creates popups in the parent's request context; signals creation.</summary>
internal sealed class PopupCaptureHandler : CefSharp.Handler.LifeSpanHandler
{
    private readonly TaskCompletionSource<ChromiumWebBrowser> _popup = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<ChromiumWebBrowser> PopupTask => _popup.Task;

    protected override bool OnBeforePopup(IWebBrowser chromiumWebBrowser, IBrowser browser, IFrame frame,
        string targetUrl, string targetFrameName, WindowOpenDisposition targetDisposition,
        bool userGesture, IPopupFeatures popupFeatures, IWindowInfo windowInfo,
        IBrowserSettings browserSettings, ref bool noJavascriptAccess, out IWebBrowser newBrowser)
    {
        // Share the parent's request context so the popup stays in the same profile.
        var popup = new ChromiumWebBrowser(targetUrl, requestContext: browser.GetHost().RequestContext);
        newBrowser = popup;
        _popup.TrySetResult(popup);
        return false;
    }
}

/// <summary>Signals when the renderer process terminates unexpectedly.</summary>
internal sealed class RenderTerminationHandler : CefSharp.Handler.RequestHandler
{
    private readonly TaskCompletionSource<CefTerminationStatus> _terminated = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public Task<CefTerminationStatus> Terminated => _terminated.Task;

    protected override void OnRenderProcessTerminated(IWebBrowser chromiumWebBrowser, IBrowser browser,
        CefTerminationStatus status, int errorCode, string errorMessage)
    {
        _terminated.TrySetResult(status);
    }
}
