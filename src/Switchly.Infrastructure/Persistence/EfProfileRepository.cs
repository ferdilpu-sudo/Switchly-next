using Microsoft.EntityFrameworkCore;
using Switchly.Application.Profiles;
using Switchly.Domain.Profiles;

namespace Switchly.Infrastructure.Persistence;

/// <summary>EF Core SQLite implementation of IProfileRepository (ADR-004).</summary>
public sealed class EfProfileRepository : IProfileRepository
{
    private readonly SwitchlyDbContext _db;

    public EfProfileRepository(SwitchlyDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<BrowserProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default)
        => _db.Profiles.FirstOrDefaultAsync(p => p.ProfileId == profileId, cancellationToken);

    public async Task<IReadOnlyList<BrowserProfile>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Profiles.AsNoTracking();
        if (!includeArchived)
        {
            query = query.Where(p => !p.IsArchived);
        }

        return await query
            .OrderByDescending(p => p.IsFavorite)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(BrowserProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        await _db.Profiles.AddAsync(profile, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BrowserProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _db.Profiles.Update(profile);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(BrowserProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        _db.Profiles.Remove(profile);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
