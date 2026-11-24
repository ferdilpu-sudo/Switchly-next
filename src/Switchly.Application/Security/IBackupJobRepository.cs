using Switchly.Domain.Security;

namespace Switchly.Application.Security;

public interface IBackupJobRepository
{
    Task AddAsync(BackupJob job, CancellationToken cancellationToken = default);

    Task UpdateAsync(BackupJob job, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BackupJob>> ListRecentAsync(int limit = 20, CancellationToken cancellationToken = default);
}
