using Switchly.Domain.History;

namespace Switchly.Application.Browser;

/// <summary>Persistence port for profile-scoped HistoryEntry records (ENT-006).</summary>
public interface IHistoryRepository
{
    Task<HistoryEntry?> FindByProfileAndUrlAsync(Guid profileId, string sanitizedUrl, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoryEntry>> ListByProfileAsync(Guid profileId, string? searchText = null, int limit = 200, CancellationToken cancellationToken = default);

    Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default);

    Task UpdateAsync(HistoryEntry entry, CancellationToken cancellationToken = default);

    Task ClearByProfileAsync(Guid profileId, CancellationToken cancellationToken = default);
}
