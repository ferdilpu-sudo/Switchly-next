using Switchly.Domain.Vault;

namespace Switchly.Application.Vault;

public interface IVaultCredentialRepository
{
    Task<IReadOnlyList<VaultCredential>> ListByProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VaultCredential>> ListByProfileAndOriginAsync(Guid profileId, string normalizedOrigin, CancellationToken cancellationToken = default);

    Task<VaultCredential?> GetByIdAsync(Guid credentialId, CancellationToken cancellationToken = default);

    Task AddAsync(VaultCredential credential, CancellationToken cancellationToken = default);

    Task UpdateAsync(VaultCredential credential, CancellationToken cancellationToken = default);

    Task DeleteAsync(VaultCredential credential, CancellationToken cancellationToken = default);
}
