using Switchly.Domain.Import;

namespace Switchly.Application.Import;

public interface IImportJobRepository
{
    Task AddAsync(ImportJob job, CancellationToken cancellationToken = default);

    Task UpdateAsync(ImportJob job, CancellationToken cancellationToken = default);

    Task<ImportJob?> GetByIdAsync(Guid importJobId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ImportJob>> ListRecentAsync(int limit = 20, CancellationToken cancellationToken = default);
}
