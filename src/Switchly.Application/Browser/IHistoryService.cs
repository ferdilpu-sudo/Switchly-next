using Switchly.Domain.History;

namespace Switchly.Application.Browser;

/// <summary>Profile-scoped browsing history use cases (REQ-006, AC-007).</summary>
public interface IHistoryService
{
    Task<HistoryEntry> RecordVisitAsync(
        Guid profileId,
        string url,
        string? title = null,
        HistoryTransitionKind transitionKind = HistoryTransitionKind.Link,
        bool retainQueryString = false,
        CancellationToken cancellationToken = default);

    Task UpdateTitleAsync(
        Guid profileId,
        string url,
        string title,
        bool retainQueryString = false,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<HistoryEntry>> ListAsync(
        Guid profileId,
        string? searchText = null,
        int limit = 200,
        CancellationToken cancellationToken = default);

    Task ClearProfileHistoryAsync(Guid profileId, CancellationToken cancellationToken = default);
}
