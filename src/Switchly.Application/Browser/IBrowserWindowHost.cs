namespace Switchly.Application.Browser;

/// <summary>Hosts a browser tab/surface bound to an isolated profile context (REQ-005, REQ-006).</summary>
public interface IBrowserWindowHost : IDisposable
{
    void Navigate(string url);

    void GoBack();

    void GoForward();

    void Reload();

    /// <summary>
    /// Explicitly fills a visible login form on the current page when triggered by user click (D6).
    /// Never exposes any persistent vault bridge to website JavaScript.
    /// </summary>
    Task<bool> FillLoginFormAsync(string username, string password);

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
}
