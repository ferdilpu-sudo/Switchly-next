using Microsoft.EntityFrameworkCore;
using Switchly.Application.Groups;
using Switchly.Domain.Groups;

namespace Switchly.Infrastructure.Persistence;

/// <summary>EF Core SQLite implementation of IGroupRepository (ENT-002, ENT-003).</summary>
public sealed class EfGroupRepository : IGroupRepository
{
    private readonly SwitchlyDbContext _db;

    public EfGroupRepository(SwitchlyDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<ProfileGroup?> GetByIdAsync(Guid groupId, CancellationToken cancellationToken = default)
        => _db.Groups.FirstOrDefaultAsync(g => g.GroupId == groupId, cancellationToken);

    public Task<ProfileGroup?> GetWebsiteDomainGroupAsync(string normalizedDomainKey, CancellationToken cancellationToken = default)
        => _db.Groups.FirstOrDefaultAsync(
            g => g.Kind == GroupKind.WebsiteDomain && g.DomainKey == normalizedDomainKey,
            cancellationToken);

    public async Task<IReadOnlyList<ProfileGroup>> GetAllAsync(bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Groups.AsNoTracking();
        if (!includeHidden)
        {
            query = query.Where(g => !g.IsHidden);
        }

        return await query
            .OrderBy(g => g.Kind)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(ProfileGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        await _db.Groups.AddAsync(group, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ProfileGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        _db.Groups.Update(group);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(ProfileGroup group, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(group);
        _db.Groups.Remove(group);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<bool> MembershipExistsAsync(Guid profileId, Guid groupId, CancellationToken cancellationToken = default)
        => _db.GroupMemberships.AnyAsync(m => m.ProfileId == profileId && m.GroupId == groupId, cancellationToken);

    public async Task AddMembershipAsync(ProfileGroupMembership membership, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);
        await _db.GroupMemberships.AddAsync(membership, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMembershipAsync(Guid profileId, Guid groupId, CancellationToken cancellationToken = default)
    {
        var membership = await _db.GroupMemberships
            .FirstOrDefaultAsync(m => m.ProfileId == profileId && m.GroupId == groupId, cancellationToken);
        if (membership is not null)
        {
            _db.GroupMemberships.Remove(membership);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Guid>> GetProfileIdsInGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
        => await _db.GroupMemberships
            .AsNoTracking()
            .Where(m => m.GroupId == groupId)
            .Select(m => m.ProfileId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProfileGroup>> GetGroupsForProfileAsync(
        Guid profileId,
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        var query = from m in _db.GroupMemberships.AsNoTracking()
                    join g in _db.Groups.AsNoTracking() on m.GroupId equals g.GroupId
                    where m.ProfileId == profileId && (includeHidden || !g.IsHidden)
                    orderby g.Kind, g.Name
                    select g;

        return await query.ToListAsync(cancellationToken);
    }
}
