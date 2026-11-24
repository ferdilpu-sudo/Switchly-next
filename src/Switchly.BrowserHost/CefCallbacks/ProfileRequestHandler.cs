using CefSharp;
using CefSharp.Handler;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Observes CEF renderer process terminations so a crashed tab surfaces non-modal recovery UI
/// without affecting other profiles or the desktop shell (TASK-035, REQ-005).
/// </summary>
internal sealed class ProfileRequestHandler : RequestHandler
{
    private readonly Action<string> _onRendererCrashed;

    public ProfileRequestHandler(Action<string> onRendererCrashed)
    {
        _onRendererCrashed = onRendererCrashed ?? throw new ArgumentNullException(nameof(onRendererCrashed));
    }

    protected override void OnRenderProcessTerminated(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        CefTerminationStatus status,
        int errorCode,
        string errorMessage)
    {
        var reason = status switch
        {
            CefTerminationStatus.ProcessCrashed => "Proses renderer berhenti tiba-tiba (crash).",
            CefTerminationStatus.ProcessWasKilled => "Proses renderer dihentikan oleh sistem.",
            CefTerminationStatus.OutOfMemory => "Proses renderer kehabisan memori (Out of Memory).",
            _ => "Proses renderer berhenti merespons.",
        };

        _onRendererCrashed(reason);
    }
}
