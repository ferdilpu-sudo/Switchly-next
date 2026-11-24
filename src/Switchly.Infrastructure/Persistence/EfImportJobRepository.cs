using Microsoft.EntityFrameworkCore;
using Switchly.Application.Import;
using Switchly.Domain.Import;

namespace Switchly.Infrastructure.Persistence;

public sealed class EfImportJobRepository : IImportJobRepository
{
    private readonly SwitchlyDbContext _db;

    public EfImportJobRepository(SwitchlyDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task AddAsync(ImportJob job, CancellationToken cancellationToken = default)
    {
        _db.ImportJobs.Add(job);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default)
    {
        _db.ImportJobs.Update(job);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public Task<ImportJob?> GetByIdAsync(Guid importJobId, CancellationToken cancellationToken = default)
        => _db.ImportJobs.FirstOrDefaultAsync(j => j.ImportJobId == importJobId, cancellationToken);

    public async Task<IReadOnlyList<ImportJob>> ListRecentAsync(int limit = 20, CancellationToken cancellationToken = default)
    {
        var items = await _db.ImportJobs
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return items
            .OrderByDescending(j => j.CreatedAtUtc)
            .Take(Math.Clamp(limit, 1, 100))
            .ToList();
    }
}
