using Switchly.Domain.Downloads;

namespace Switchly.Application.Browser;

/// <summary>Persistence port for profile-scoped DownloadRecord rows (ENT-007).</summary>
public interface IDownloadRepository
{
    Task<DownloadRecord?> FindByProfileAndTargetPathAsync(Guid profileId, string targetPath, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DownloadRecord>> ListByProfileAsync(Guid profileId, int limit = 100, CancellationToken cancellationToken = default);

    Task AddAsync(DownloadRecord record, CancellationToken cancellationToken = default);

    Task UpdateAsync(DownloadRecord record, CancellationToken cancellationToken = default);

    Task MarkAllInProgressAsFailedAsync(DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default);
}
