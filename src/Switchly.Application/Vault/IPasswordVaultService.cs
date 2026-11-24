namespace Switchly.Application.Vault;

/// <summary>
/// Manages per-profile encrypted credentials (ENT-009), Chrome/Edge CSV import (TASK-028),
/// and exact-origin autofill lookup (D6).
/// </summary>
public interface IPasswordVaultService
{
    Task<VaultCredentialSummary> UpsertCredentialAsync(
        Guid profileId,
        string urlOrOrigin,
        string username,
        string password,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VaultCredentialSummary>> ListSummariesAsync(
        Guid profileId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VaultCredentialMatch>> FindMatchesForUrlAsync(
        Guid profileId,
        string currentPageUrl,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid profileId,
        Guid credentialId,
        CancellationToken cancellationToken = default);

    Task<CsvPasswordImportResult> ImportChromeEdgeCsvAsync(
        Guid profileId,
        string csvFilePath,
        CancellationToken cancellationToken = default);
}
