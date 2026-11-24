using Switchly.Domain.Tabs;

namespace Switchly.Application.Browser;

/// <summary>Profile-scoped tab session persistence and restore use cases (REQ-006, AC-007).</summary>
public interface ITabSessionService
{
    Task SaveOpenTabsAsync(Guid profileId, Guid windowId, IReadOnlyList<TabSnapshotInput> tabs, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BrowserTabSession>> GetRestorableTabsAsync(Guid profileId, CancellationToken cancellationToken = default);
}
