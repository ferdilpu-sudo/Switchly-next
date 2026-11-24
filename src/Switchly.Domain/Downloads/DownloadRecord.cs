namespace Switchly.Domain.Downloads;

public enum DownloadState
{
    InProgress,
    Completed,
    Cancelled,
    Failed,
}

/// <summary>Profile-scoped download metadata record (ENT-007, REQ-006).</summary>
public sealed class DownloadRecord
{
    public Guid DownloadId { get; set; }

    public Guid ProfileId { get; set; }

    public string SuggestedFileName { get; set; } = string.Empty;

    public string TargetPath { get; set; } = string.Empty;

    public string? MimeType { get; set; }

    public long? BytesReceived { get; set; }

    public long? TotalBytes { get; set; }

    public DownloadState State { get; set; } = DownloadState.InProgress;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
