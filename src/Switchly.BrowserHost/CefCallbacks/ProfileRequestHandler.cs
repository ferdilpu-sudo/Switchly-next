using CefSharp;
using CefSharp.Handler;
using Switchly.Domain.Browser;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Enforces navigation URL scheme policy inside CEF (blocking file:, chrome:, data: top-level,
/// and external OS protocol invocations) and observes renderer crashes (TASK-035, REQ-005).
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
        if (request is null)
        {
            return false;
        }

        var url = request.Url?.Trim() ?? string.Empty;
        if (frame is { IsMain: true })
        {
            // Main frame: allow http/https, about:blank, and blob: (block top-level data:, file:, chrome:, custom OS schemes).
            var isAllowedMain = BrowserUrlPolicy.IsAllowedNavigationUrl(url)
                || url.StartsWith("blob:", StringComparison.OrdinalIgnoreCase);

            return !isAllowedMain;
        }

        // Subframe: allow http/https, about:blank/about:srcdoc, blob:, and data: media/frames; block file:, chrome:, devtools:, OS schemes.
        var isAllowedSubframe = BrowserUrlPolicy.IsAllowedDownloadUrl(url)
            || string.Equals(url, "about:srcdoc", StringComparison.OrdinalIgnoreCase);

        return !isAllowedSubframe;
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
