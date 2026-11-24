using Switchly.Domain.Groups;

namespace Switchly.Application.Groups;

/// <summary>Application use cases for manual and automatic WebsiteDomain grouping (REQ-004, AC-005).</summary>
public interface IGroupService
{
    Task<ProfileGroup> CreateManualGroupAsync(string name, CancellationToken cancellationToken = default);

    Task<ProfileGroup> RenameManualGroupAsync(Guid groupId, string newName, CancellationToken cancellationToken = default);

    Task DeleteManualGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<ProfileGroup> SetGroupHiddenAsync(Guid groupId, bool isHidden, CancellationToken cancellationToken = default);

    Task AssignProfileToManualGroupAsync(Guid profileId, Guid groupId, CancellationToken cancellationToken = default);

    Task RemoveProfileFromManualGroupAsync(Guid profileId, Guid groupId, CancellationToken cancellationToken = default);

    Task<ProfileGroup> SyncWebsiteDomainGroupAsync(Guid profileId, string urlOrHost, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProfileGroup>> ListGroupsAsync(bool includeHidden = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetProfileIdsInGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProfileGroup>> GetGroupsForProfileAsync(Guid profileId, bool includeHidden = false, CancellationToken cancellationToken = default);
}
