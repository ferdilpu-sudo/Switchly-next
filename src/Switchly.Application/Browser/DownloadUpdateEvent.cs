using Switchly.Domain.Downloads;

namespace Switchly.Application.Browser;

public sealed record DownloadUpdateEvent(
    Guid ProfileId,
    string SuggestedFileName,
    string TargetPath,
    string? MimeType,
    long BytesReceived,
    long? TotalBytes,
    DownloadState State);
