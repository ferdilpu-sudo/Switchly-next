using System.IO;
using CefSharp;
using Switchly.Application.Browser;
using Switchly.Domain.Downloads;

namespace Switchly.BrowserHost.CefCallbacks;

/// <summary>
/// Profile-scoped CEF IDownloadHandler (REQ-006, TEST-007, TEST-021):
/// sanitizes filenames, keeps transfers inside per-profile download directories,
/// and reports state transitions without leaking CefSharp types.
/// </summary>
internal sealed class ProfileDownloadHandler : IDownloadHandler
{
    private readonly Guid _profileId;
    private readonly string _downloadDirectory;
    private readonly Action<DownloadUpdateEvent> _onDownloadUpdated;

    public ProfileDownloadHandler(
        Guid profileId,
        Action<DownloadUpdateEvent> onDownloadUpdated,
        string? baseDownloadDirectory = null)
    {
        _profileId = profileId;
        _onDownloadUpdated = onDownloadUpdated ?? throw new ArgumentNullException(nameof(onDownloadUpdated));

        var userDownloads = string.IsNullOrWhiteSpace(baseDownloadDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "SwitchlyNext")
            : baseDownloadDirectory;
        _downloadDirectory = Path.Combine(userDownloads, profileId.ToString("N")[..8]);
    }

    public bool CanDownload(IWebBrowser chromiumWebBrowser, IBrowser browser, string url, string requestMethod) => true;

    public bool OnBeforeDownload(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        DownloadItem downloadItem,
        IBeforeDownloadCallback callback)
    {
        if (callback.IsDisposed)
        {
            return false;
        }

        Directory.CreateDirectory(_downloadDirectory);
        var safeName = DownloadFileNameSanitizer.Sanitize(downloadItem.SuggestedFileName);
        var targetPath = EnsureUniquePath(_downloadDirectory, safeName);

        using (callback)
        {
            callback.Continue(targetPath, showDialog: true);
        }

        _onDownloadUpdated(new DownloadUpdateEvent(
            _profileId,
            safeName,
            targetPath,
            downloadItem.MimeType,
            downloadItem.ReceivedBytes,
            downloadItem.TotalBytes >= 0 ? downloadItem.TotalBytes : null,
            DownloadState.InProgress));

        return true;
    }

    public void OnDownloadUpdated(
        IWebBrowser chromiumWebBrowser,
        IBrowser browser,
        DownloadItem downloadItem,
        IDownloadItemCallback callback)
    {
        if (string.IsNullOrWhiteSpace(downloadItem.FullPath))
        {
            return;
        }

        var state = downloadItem.IsComplete
            ? DownloadState.Completed
            : downloadItem.IsCancelled
                ? DownloadState.Cancelled
                : DownloadState.InProgress;

        var safeName = DownloadFileNameSanitizer.Sanitize(
            string.IsNullOrWhiteSpace(downloadItem.SuggestedFileName)
                ? Path.GetFileName(downloadItem.FullPath)
                : downloadItem.SuggestedFileName);

        _onDownloadUpdated(new DownloadUpdateEvent(
            _profileId,
            safeName,
            downloadItem.FullPath,
            downloadItem.MimeType,
            downloadItem.ReceivedBytes,
            downloadItem.TotalBytes >= 0 ? downloadItem.TotalBytes : null,
            state));
    }

    private static string EnsureUniquePath(string directory, string safeFileName)
    {
        var candidate = Path.Combine(directory, safeFileName);
        if (!File.Exists(candidate))
        {
            return candidate;
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(safeFileName);
        var ext = Path.GetExtension(safeFileName);
        for (var i = 1; i <= 9999; i++)
        {
            var next = Path.Combine(directory, $"{nameWithoutExt} ({i}){ext}");
            if (!File.Exists(next))
            {
                return next;
            }
        }

        return Path.Combine(directory, $"{nameWithoutExt}-{Guid.NewGuid():N}{ext}");
    }
}
