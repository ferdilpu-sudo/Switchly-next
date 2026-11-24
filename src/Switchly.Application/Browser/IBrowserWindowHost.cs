namespace Switchly.Application.Browser;

/// <summary>Hosts a browser tab/surface bound to an isolated profile context (REQ-005, REQ-006).</summary>
public interface IBrowserWindowHost : IDisposable
{
    void Navigate(string url);

    void GoBack();

    void GoForward();

    void Reload();

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
}
