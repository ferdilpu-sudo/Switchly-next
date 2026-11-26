using CefSharp;
using CefSharp.Handler;
using Switchly.Domain.Browser;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Enforces top-level navigation URL scheme policy inside CEF and observes renderer crashes
/// so a crashed tab surfaces non-modal recovery UI without affecting other profiles (TASK-035, REQ-005).
/// </summary>
internal sealed class ProfileRequestHandler : RequestHandler
{
    private readonly Action<string> _onRendererCrashed;

    public ProfileRequestHandler(Action<string> onRendererCrashed)
    {
        _onRendererCrashed = onRendererCrashed ?? throw new ArgumentNullException(nameof(onRendererCrashed));
    }

    protected override bool OnBeforeBrowse(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        IFrame frame,
        IRequest request,
        bool userGesture,
        bool isRedirect)
    {
        if (frame is { IsMain: true } && request is not null)
        {
            var url = request.Url;
            // Allow http/https/about:blank and blob/data URLs used by in-browser media/PDF viewers;
            // block local filesystem and internal Chromium schemes (file:, chrome:, chrome-extension:, etc.).
            if (!BrowserUrlPolicy.IsAllowedDownloadUrl(url) &&
                !string.Equals(url?.Trim(), BrowserUrlPolicy.BlankUrl, StringComparison.OrdinalIgnoreCase))
            {
                return true; // Cancel disallowed top-level navigation.
            }
        }

        return false;
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
