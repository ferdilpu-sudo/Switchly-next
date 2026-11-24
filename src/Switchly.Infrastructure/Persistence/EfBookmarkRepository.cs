using Microsoft.EntityFrameworkCore;
using Switchly.Application.Browser;
using Switchly.Domain.Bookmarks;

namespace Switchly.Infrastructure.Persistence;

public sealed class EfBookmarkRepository : IBookmarkRepository
{
    private readonly SwitchlyDbContext _db;

    public EfBookmarkRepository(SwitchlyDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<Bookmark?> GetByIdAsync(Guid bookmarkId, CancellationToken cancellationToken = default)
        => _db.Bookmarks.FirstOrDefaultAsync(b => b.BookmarkId == bookmarkId, cancellationToken);

    public async Task<IReadOnlyList<Bookmark>> ListByProfileAsync(
        Guid profileId,
        string? searchText = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Bookmarks.AsNoTracking().Where(b => b.ProfileId == profileId);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            var term = searchText.Trim().ToLowerInvariant();
            query = query.Where(b =>
                b.Title.ToLower().Contains(term) ||
                (b.Url != null && b.Url.ToLower().Contains(term)));
        }

        return await query
            .OrderBy(b => b.ParentBookmarkId)
            .ThenBy(b => b.Position)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetNextPositionAsync(Guid profileId, Guid? parentBookmarkId, CancellationToken cancellationToken = default)
    {
        var max = await _db.Bookmarks
            .Where(b => b.ProfileId == profileId && b.ParentBookmarkId == parentBookmarkId)
            .Select(b => (int?)b.Position)
            .MaxAsync(cancellationToken);
        return max.HasValue ? max.Value + 1 : 0;
    }

    public async Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
    {
        await _db.Bookmarks.AddAsync(bookmark, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Bookmark bookmark, CancellationToken cancellationToken = default)
    {
        _db.Bookmarks.Remove(bookmark);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
