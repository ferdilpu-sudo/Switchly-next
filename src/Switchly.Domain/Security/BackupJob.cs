namespace Switchly.Domain.Security;

public enum BackupMode
{
    Manual = 0,
}

public enum BackupJobStatus
{
    Queued = 0,
    Running = 1,
    Completed = 2,
    Failed = 3,
    Cancelled = 4,
}

/// <summary>Audit record for manual encrypted backup runs (ENT-011). Never stores passphrases or keys.</summary>
public sealed class BackupJob
{
    public Guid BackupJobId { get; set; }

    public BackupMode Mode { get; set; } = BackupMode.Manual;

    public BackupJobStatus Status { get; set; } = BackupJobStatus.Queued;

    public string DestinationPath { get; set; } = string.Empty;

    public DateTimeOffset? StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public string IncludedProfilesJson { get; set; } = "[]";

    public int ArchiveVersion { get; set; } = BackupLimits.CurrentArchiveVersion;

    public string ResultSummaryJson { get; set; } = "{}";
}
