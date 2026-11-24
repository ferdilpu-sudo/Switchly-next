using Microsoft.EntityFrameworkCore;
using Switchly.Application.Browser;
using Switchly.Domain.History;

namespace Switchly.Infrastructure.Persistence;

public sealed class EfHistoryRepository : IHistoryRepository
{
    private readonly SwitchlyDbContext _db;

    public EfHistoryRepository(SwitchlyDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<HistoryEntry?> FindByProfileAndUrlAsync(
        Guid profileId,
        string sanitizedUrl,
        CancellationToken cancellationToken = default)
        => _db.HistoryEntries.FirstOrDefaultAsync(
            h => h.ProfileId == profileId && h.Url == sanitizedUrl,
            cancellationToken);

    public async Task<IReadOnlyList<HistoryEntry>> ListByProfileAsync(
        Guid profileId,
        string? searchText = null,
        int limit = 200,
        CancellationToken cancellationToken = default)
    {
        var query = _db.HistoryEntries.AsNoTracking().Where(h => h.ProfileId == profileId);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim().ToLowerInvariant();
            query = query.Where(h =>
                h.Url.ToLower().Contains(term) ||
                (h.Title != null && h.Title.ToLower().Contains(term)));
        }

        // Order client-side for SQLite DateTimeOffset compatibility.
        var rows = await query.ToListAsync(cancellationToken);
        return rows
            .OrderByDescending(h => h.VisitedAtUtc)
            .Take(limit)
            .ToList();
    }

    public async Task AddAsync(HistoryEntry entry, CancellationToken cancellationToken = default)
    {
        await _db.HistoryEntries.AddAsync(entry, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(HistoryEntry entry, CancellationToken cancellationToken = default)
    {
        _db.HistoryEntries.Update(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task ClearByProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var entries = await _db.HistoryEntries.Where(h => h.ProfileId == profileId).ToListAsync(cancellationToken);
        if (entries.Count > 0)
        {
            _db.HistoryEntries.RemoveRange(entries);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
