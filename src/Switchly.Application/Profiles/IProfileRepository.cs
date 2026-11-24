using Switchly.Domain.Profiles;

namespace Switchly.Application.Profiles;

/// <summary>Persistence port for BrowserProfile metadata (ENT-001).</summary>
public interface IProfileRepository
{
    Task<BrowserProfile?> GetByIdAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BrowserProfile>> GetAllAsync(bool includeArchived = false, CancellationToken cancellationToken = default);

    Task AddAsync(BrowserProfile profile, CancellationToken cancellationToken = default);

    Task UpdateAsync(BrowserProfile profile, CancellationToken cancellationToken = default);

    Task DeleteAsync(BrowserProfile profile, CancellationToken cancellationToken = default);
}
