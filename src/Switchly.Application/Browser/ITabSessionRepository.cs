using Switchly.Domain.Tabs;

namespace Switchly.Application.Browser;

public sealed record TabSnapshotInput(string Url, string? Title, bool IsPinned = false);

/// <summary>Persistence port for profile-scoped BrowserTabSession rows (ENT-004).</summary>
public interface ITabSessionRepository
{
    Task<IReadOnlyList<BrowserTabSession>> ListOpenTabsByProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task ReplaceOpenTabsForProfileAsync(Guid profileId, IReadOnlyList<BrowserTabSession> tabs, CancellationToken cancellationToken = default);
}
