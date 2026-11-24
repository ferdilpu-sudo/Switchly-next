using Microsoft.EntityFrameworkCore;
using Switchly.Application.Vault;
using Switchly.Domain.Vault;

namespace Switchly.Infrastructure.Persistence;

public sealed class EfVaultCredentialRepository : IVaultCredentialRepository
{
    private readonly SwitchlyDbContext _db;

    public EfVaultCredentialRepository(SwitchlyDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<VaultCredential>> ListByProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        var list = await _db.VaultCredentials
            .Where(c => c.ProfileId == profileId)
            .ToListAsync(cancellationToken);

        return list
            .OrderBy(c => c.Origin, StringComparer.OrdinalIgnoreCase)
            .ThenByDescending(c => c.UpdatedAtUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<VaultCredential>> ListByProfileAndOriginAsync(
        Guid profileId,
        string normalizedOrigin,
        CancellationToken cancellationToken = default)
        => await _db.VaultCredentials
            .Where(c => c.ProfileId == profileId && c.Origin == normalizedOrigin)
            .ToListAsync(cancellationToken);

    public Task<VaultCredential?> GetByIdAsync(Guid credentialId, CancellationToken cancellationToken = default)
        => _db.VaultCredentials.FirstOrDefaultAsync(c => c.CredentialId == credentialId, cancellationToken);

    public async Task AddAsync(VaultCredential credential, CancellationToken cancellationToken = default)
    {
        _db.VaultCredentials.Add(credential);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(VaultCredential credential, CancellationToken cancellationToken = default)
    {
        _db.VaultCredentials.Update(credential);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(VaultCredential credential, CancellationToken cancellationToken = default)
    {
        _db.VaultCredentials.Remove(credential);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
