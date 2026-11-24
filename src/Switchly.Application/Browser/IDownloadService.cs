using Switchly.Domain.Downloads;

namespace Switchly.Application.Browser;

/// <summary>Profile-scoped download metadata service (REQ-006, AC-007).</summary>
public interface IDownloadService
{
    Task<DownloadRecord> RecordOrUpdateAsync(DownloadUpdateEvent update, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DownloadRecord>> ListByProfileAsync(Guid profileId, int limit = 100, CancellationToken cancellationToken = default);

    Task MarkInterruptedOnStartupAsync(CancellationToken cancellationToken = default);
}
