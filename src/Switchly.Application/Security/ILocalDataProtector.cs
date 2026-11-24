namespace Switchly.Application.Security;

/// <summary>
/// Local OS-bound data protection boundary (Windows DPAPI CurrentUser, ADR-005).
/// Must fail closed if decryption is attempted under a different user account or corrupted blob.
/// </summary>
public interface ILocalDataProtector
{
    byte[] Protect(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> entropy = default);

    byte[] Unprotect(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> entropy = default);
}
