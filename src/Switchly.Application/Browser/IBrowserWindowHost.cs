namespace Switchly.Application.Browser;

public enum BrowserShortcutCommand
{
    NewTab,
    CloseTab,
    ReopenClosedTab,
    FocusAddressBar,
    Reload,
    NextTab,
    PreviousTab,
    ZoomIn,
    ZoomOut,
    ResetZoom,
    QuickSearch,
    FindInPage,
}

/// <summary>Hosts a browser tab/surface bound to an isolated profile context (REQ-005, REQ-006).</summary>
public interface IBrowserWindowHost : IDisposable
{
    void Navigate(string url);

    void GoBack();

    void GoForward();

    void Reload();

    void Stop();

    void ZoomIn();

    void ZoomOut();

    void ResetZoom();

    void Find(string searchText, bool forward, bool matchCase, bool findNext);

    void StopFinding(bool clearSelection);

    /// <summary>
    /// Explicitly fills a visible login form on the current page when triggered by user click (D6).
    /// Re-verifies <paramref name="expectedOrigin"/> inside the page (TOCTOU defense-in-depth).
    /// Never exposes any persistent vault bridge to website JavaScript.
    /// </summary>
    Task<bool> FillLoginFormAsync(string username, string password, string? expectedOrigin = null);

    bool CanGoBack { get; }

    bool CanGoForward { get; }

    bool IsLoading { get; }

    string? Address { get; }

    string? Title { get; }

    event Action<string>? AddressChanged;

    event Action<string>? TitleChanged;

    event Action<bool>? LoadingStateChanged;

    /// <summary>Raised when a page requests a new tab/window (target=_blank / window.open) within the same profile context.</summary>
    event Action<string>? NewTabRequested;

    /// <summary>Raised when a download starts, progresses, completes, or cancels within this profile context.</summary>
    event Action<DownloadUpdateEvent>? DownloadUpdated;

    /// <summary>Raised when the tab's browser renderer process terminates abnormally (TASK-035).</summary>
    event Action<string>? RendererCrashed;

    /// <summary>Raised when a browser keyboard shortcut is captured inside the CEF surface.</summary>
    event Action<BrowserShortcutCommand>? ShortcutPressed;

    /// <summary>Raised when hovering links or page status text changes (bottom-left URL preview bubble).</summary>
    event Action<string>? StatusMessageChanged;

    /// <summary>Raised when zoom percentage changes (e.g., 100, 110, 90).</summary>
    event Action<int>? ZoomPercentChanged;

    /// <summary>Raised when an HTML5 element (such as a video player) enters or exits fullscreen mode.</summary>
    event Action<bool>? FullscreenModeChanged;
}
