using Microsoft.EntityFrameworkCore;
using Switchly.Application.Security;
using Switchly.Domain.Security;

namespace Switchly.Infrastructure.Persistence;

public sealed class EfBackupJobRepository : IBackupJobRepository
{
    private readonly SwitchlyDbContext _db;

    public EfBackupJobRepository(SwitchlyDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task AddAsync(BackupJob job, CancellationToken cancellationToken = default)
    {
        _db.BackupJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(BackupJob job, CancellationToken cancellationToken = default)
    {
        _db.BackupJobs.Update(job);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<BackupJob>> ListRecentAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        var items = await _db.BackupJobs
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(j => j.StartedAtUtc ?? DateTimeOffset.MinValue)
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();
    }
}
