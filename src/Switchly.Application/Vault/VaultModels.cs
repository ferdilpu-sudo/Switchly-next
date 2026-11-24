namespace Switchly.Application.Vault;

/// <summary>Non-secret projection for displaying saved credentials in UI lists (never contains plaintext password).</summary>
public sealed record VaultCredentialSummary(
    Guid CredentialId,
    Guid ProfileId,
    string Origin,
    string Username,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Decrypted match returned ONLY when the user explicitly triggers autofill for an exact origin.</summary>
public sealed record VaultCredentialMatch(
    Guid CredentialId,
    string Origin,
    string Username,
    string Password);

/// <summary>Redacted summary returned after Chrome/Edge CSV password import (TASK-028, TEST-010, TEST-022).</summary>
public sealed record CsvPasswordImportResult(
    int ImportedCount,
    int UpdatedCount,
    int SkippedCount,
    int MalformedRowCount,
    IReadOnlyList<string> RedactedWarnings);
