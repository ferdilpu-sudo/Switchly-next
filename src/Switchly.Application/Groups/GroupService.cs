using Switchly.Application.Profiles;
using Switchly.Domain.Groups;

namespace Switchly.Application.Groups;

/// <summary>Implements manual and system-derived WebsiteDomain profile grouping (REQ-004, AC-005).</summary>
public sealed class GroupService : IGroupService
{
    private readonly IGroupRepository _groups;
    private readonly IProfileRepository _profiles;
    private readonly TimeProvider _timeProvider;

    public GroupService(
        IGroupRepository groups,
        IProfileRepository profiles,
        TimeProvider? timeProvider = null)
    {
        _groups = groups ?? throw new ArgumentNullException(nameof(groups));
        _profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<ProfileGroup> CreateManualGroupAsync(string name, CancellationToken cancellationToken = default)
    {
        var now = _timeProvider.GetUtcNow();
        var group = new ProfileGroup
        {
            GroupId = Guid.NewGuid(),
            Name = GroupValidation.NormalizeAndValidateName(name),
            Kind = GroupKind.Manual,
            DomainKey = null,
            IsHidden = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await _groups.AddAsync(group, cancellationToken);
        return group;
    }

    public async Task<ProfileGroup> RenameManualGroupAsync(Guid groupId, string newName, CancellationToken cancellationToken = default)
    {
        var group = await _groups.GetByIdAsync(groupId, cancellationToken)
            ?? throw new GroupNotFoundException(groupId);

        if (group.Kind == GroupKind.WebsiteDomain)
        {
            throw new SystemGroupReadOnlyException(groupId, "rename");
        }

        group.Name = GroupValidation.NormalizeAndValidateName(newName);
        group.UpdatedAtUtc = _timeProvider.GetUtcNow();
        await _groups.UpdateAsync(group, cancellationToken);
        return group;
    }

    public async Task DeleteManualGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _groups.GetByIdAsync(groupId, cancellationToken)
            ?? throw new GroupNotFoundException(groupId);

        if (group.Kind == GroupKind.WebsiteDomain)
        {
            throw new SystemGroupReadOnlyException(groupId, "delete");
        }

        await _groups.DeleteAsync(group, cancellationToken);
    }

    public async Task<ProfileGroup> SetGroupHiddenAsync(Guid groupId, bool isHidden, CancellationToken cancellationToken = default)
    {
        var group = await _groups.GetByIdAsync(groupId, cancellationToken)
            ?? throw new GroupNotFoundException(groupId);

        group.IsHidden = isHidden;
        group.UpdatedAtUtc = _timeProvider.GetUtcNow();
        await _groups.UpdateAsync(group, cancellationToken);
        return group;
    }

    public async Task AssignProfileToManualGroupAsync(Guid profileId, Guid groupId, CancellationToken cancellationToken = default)
    {
        _ = await _profiles.GetByIdAsync(profileId, cancellationToken)
            ?? throw new ProfileNotFoundException(profileId);

        var group = await _groups.GetByIdAsync(groupId, cancellationToken)
            ?? throw new GroupNotFoundException(groupId);

        if (group.Kind == GroupKind.WebsiteDomain)
        {
            throw new SystemGroupReadOnlyException(groupId, "manual-assign");
        }

        if (!await _groups.MembershipExistsAsync(profileId, groupId, cancellationToken))
        {
            await _groups.AddMembershipAsync(
                new ProfileGroupMembership
                {
                    ProfileId = profileId,
                    GroupId = groupId,
                    MembershipSource = MembershipSource.Manual,
                },
                cancellationToken);
        }
    }

    public async Task RemoveProfileFromManualGroupAsync(Guid profileId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var group = await _groups.GetByIdAsync(groupId, cancellationToken)
            ?? throw new GroupNotFoundException(groupId);

        if (group.Kind == GroupKind.WebsiteDomain)
        {
            throw new SystemGroupReadOnlyException(groupId, "manual-remove");
        }

        await _groups.RemoveMembershipAsync(profileId, groupId, cancellationToken);
    }

    public async Task<ProfileGroup> SyncWebsiteDomainGroupAsync(Guid profileId, string urlOrHost, CancellationToken cancellationToken = default)
    {
        _ = await _profiles.GetByIdAsync(profileId, cancellationToken)
            ?? throw new ProfileNotFoundException(profileId);

        var domainKey = GroupValidation.NormalizeDomainKey(urlOrHost);
        var group = await _groups.GetWebsiteDomainGroupAsync(domainKey, cancellationToken);

        if (group is null)
        {
            var now = _timeProvider.GetUtcNow();
            var displayName = domainKey.Length > GroupValidation.MaxNameLength
                ? domainKey[..GroupValidation.MaxNameLength]
                : domainKey;

            group = new ProfileGroup
            {
                GroupId = Guid.NewGuid(),
                Name = displayName,
                Kind = GroupKind.WebsiteDomain,
                DomainKey = domainKey,
                IsHidden = false,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            await _groups.AddAsync(group, cancellationToken);
        }

        if (!await _groups.MembershipExistsAsync(profileId, group.GroupId, cancellationToken))
        {
            await _groups.AddMembershipAsync(
                new ProfileGroupMembership
                {
                    ProfileId = profileId,
                    GroupId = group.GroupId,
                    MembershipSource = MembershipSource.Auto,
                },
                cancellationToken);
        }

        return group;
    }

    public Task<IReadOnlyList<ProfileGroup>> ListGroupsAsync(bool includeHidden = false, CancellationToken cancellationToken = default)
        => _groups.GetAllAsync(includeHidden, cancellationToken);

    public Task<IReadOnlyList<Guid>> GetProfileIdsInGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        => _groups.GetProfileIdsInGroupAsync(groupId, cancellationToken);

    public Task<IReadOnlyList<ProfileGroup>> GetGroupsForProfileAsync(Guid profileId, bool includeHidden = false, CancellationToken cancellationToken = default)
        => _groups.GetGroupsForProfileAsync(profileId, includeHidden, cancellationToken);
}
