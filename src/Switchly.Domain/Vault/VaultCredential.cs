namespace Switchly.Domain.Vault;

/// <summary>
/// Per-profile encrypted login credential (ENT-009, D6, ADR-005).
/// Encrypts canonical UTF-8 JSON {"username","password"} as a single AES-256-GCM payload
/// with a unique 96-bit CSPRNG nonce and AAD binding (CryptoVersion, CredentialId, ProfileId, Origin).
/// Never stores plaintext usernames or passwords in SQLite.
/// </summary>
public sealed class VaultCredential
{
    public const int CurrentCryptoVersion = 1;

    public Guid CredentialId { get; set; }

    public Guid ProfileId { get; set; }

    public string Origin { get; set; } = string.Empty;

    public byte[] SecretPayloadCiphertext { get; set; } = Array.Empty<byte>();

    public int CryptoVersion { get; set; } = CurrentCryptoVersion;

    public byte[] Nonce { get; set; } = Array.Empty<byte>();

    public byte[] AuthenticationTag { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
