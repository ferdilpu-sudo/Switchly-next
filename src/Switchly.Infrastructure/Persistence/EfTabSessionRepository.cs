using Microsoft.EntityFrameworkCore;
using Switchly.Application.Browser;
using Switchly.Domain.Tabs;

namespace Switchly.Infrastructure.Persistence;

public sealed class EfTabSessionRepository : ITabSessionRepository
{
    private readonly SwitchlyDbContext _db;

    public EfTabSessionRepository(SwitchlyDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<BrowserTabSession>> ListOpenTabsByProfileAsync(
        Guid profileId,
        CancellationToken cancellationToken = default)
        => await _db.TabSessions
            .AsNoTracking()
            .Where(t => t.ProfileId == profileId && t.RestoreState == TabRestoreState.Open)
            .OrderBy(t => t.Position)
            .ToListAsync(cancellationToken);

    public async Task ReplaceOpenTabsForProfileAsync(
        Guid profileId,
        IReadOnlyList<BrowserTabSession> tabs,
        CancellationToken cancellationToken = default)
    {
        var existing = await _db.TabSessions
            .Where(t => t.ProfileId == profileId)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            _db.TabSessions.RemoveRange(existing);
        }

        if (tabs.Count > 0)
        {
            await _db.TabSessions.AddRangeAsync(tabs, cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
