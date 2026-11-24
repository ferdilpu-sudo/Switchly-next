using Microsoft.EntityFrameworkCore;
using Switchly.Application.Browser;
using Switchly.Domain.Downloads;

namespace Switchly.Infrastructure.Persistence;

public sealed class EfDownloadRepository : IDownloadRepository
{
    private readonly SwitchlyDbContext _db;

    public EfDownloadRepository(SwitchlyDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public Task<DownloadRecord?> FindByProfileAndTargetPathAsync(
        Guid profileId,
        string targetPath,
        CancellationToken cancellationToken = default)
        => _db.Downloads.FirstOrDefaultAsync(
            d => d.ProfileId == profileId && d.TargetPath == targetPath,
            cancellationToken);

    public async Task<IReadOnlyList<DownloadRecord>> ListByProfileAsync(
        Guid profileId,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var rows = await _db.Downloads
            .AsNoTracking()
            .Where(d => d.ProfileId == profileId)
            .ToListAsync(cancellationToken);

        return rows
            .OrderByDescending(d => d.StartedAtUtc)
            .Take(limit)
            .ToList();
    }

    public async Task AddAsync(DownloadRecord record, CancellationToken cancellationToken = default)
    {
        await _db.Downloads.AddAsync(record, cancellationToken);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(DownloadRecord record, CancellationToken cancellationToken = default)
    {
        _db.Downloads.Update(record);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllInProgressAsFailedAsync(DateTimeOffset completedAtUtc, CancellationToken cancellationToken = default)
    {
        var stale = await _db.Downloads
            .Where(d => d.State == DownloadState.InProgress)
            .ToListAsync(cancellationToken);

        if (stale.Count == 0)
        {
            return;
        }

        foreach (var item in stale)
        {
            item.State = DownloadState.Failed;
            item.CompletedAtUtc = completedAtUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}
