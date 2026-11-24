namespace Switchly.Application.Security;

public sealed record BackupManifest(
    int ArchiveVersion,
    DateTimeOffset CreatedAtUtc,
    int ProfileCount,
    IReadOnlyList<Guid> IncludedProfileIds,
    long EstimatedPlaintextBytes,
    bool ExcludesDeviceBoundCookies);

public sealed record BackupProgress(
    string Stage,
    double Fraction);

public sealed record BackupCreationResult(
    Guid BackupJobId,
    string DestinationPath,
    int ProfileCount,
    long ArchiveSizeBytes,
    DateTimeOffset CompletedAtUtc);

public sealed record RestoreStageResult(
    BackupManifest Manifest,
    string StagingDirectoryPath,
    int ExtractedFileCount);

public interface IBackupRestoreService
{
    Task<BackupCreationResult> CreateBackupAsync(
        string destinationArchivePath,
        string passphrase,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);

    BackupManifest InspectBackup(string archivePath);

    Task<RestoreStageResult> StageRestoreAsync(
        string archivePath,
        string passphrase,
        IProgress<BackupProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
