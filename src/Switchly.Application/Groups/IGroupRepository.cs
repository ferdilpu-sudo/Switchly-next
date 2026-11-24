using Switchly.Domain.Groups;

namespace Switchly.Application.Groups;

/// <summary>Persistence port for ProfileGroup (ENT-002) and ProfileGroupMembership (ENT-003).</summary>
public interface IGroupRepository
{
    Task<ProfileGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<ProfileGroup?> GetWebsiteDomainGroupAsync(string normalizedDomainKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProfileGroup>> GetAllAsync(bool includeHidden = false, CancellationToken cancellationToken = default);

    Task AddAsync(ProfileGroup group, CancellationToken cancellationToken = default);

    Task UpdateAsync(ProfileGroup group, CancellationToken cancellationToken = default);

    Task DeleteAsync(ProfileGroup group, CancellationToken cancellationToken = default);

    Task<bool> MembershipExistsAsync(Guid profileId, Guid groupId, CancellationToken cancellationToken = default);

    Task AddMembershipAsync(ProfileGroupMembership membership, CancellationToken cancellationToken = default);

    Task RemoveMembershipAsync(Guid profileId, Guid groupId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> GetProfileIdsInGroupAsync(Guid groupId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ProfileGroup>> GetGroupsForProfileAsync(Guid profileId, bool includeHidden = false, CancellationToken cancellationToken = default);
}
