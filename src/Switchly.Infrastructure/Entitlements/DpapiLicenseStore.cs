using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Switchly.Application.Entitlements;
using Switchly.Application.Security;
using Switchly.Domain.Entitlements;

namespace Switchly.Infrastructure.Entitlements;

/// <summary>
/// Persists the local signed license state wrapped in Windows DPAPI (CurrentUser) with domain entropy.
/// Fails closed (returns null) if the file is missing, corrupted, or decrypted under a different Windows user.
/// </summary>
public sealed class DpapiLicenseStore : ILicenseStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Switchly.LicenseState.v1");

    private readonly string _filePath;
    private readonly ILocalDataProtector _protector;
    private readonly object _sync = new();

    public DpapiLicenseStore(string filePath, ILocalDataProtector protector)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("License file path must not be empty.", nameof(filePath));
        }

        _filePath = filePath;
        _protector = protector;
    }

    public StoredLicenseState? Load()
    {
        lock (_sync)
        {
            if (!File.Exists(_filePath))
            {
                return null;
            }

            try
            {
                var encrypted = File.ReadAllBytes(_filePath);
                var plaintext = _protector.Unprotect(encrypted, Entropy);
                try
                {
                    return JsonSerializer.Deserialize<StoredLicenseState>(plaintext);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(plaintext);
                }
            }
            catch (Exception ex) when (ex is CryptographicException or JsonException or IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
    }

    public void Save(StoredLicenseState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_sync)
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var plaintext = JsonSerializer.SerializeToUtf8Bytes(state);
            try
            {
                var encrypted = _protector.Protect(plaintext, Entropy);
                var tempPath = $"{_filePath}.{Guid.NewGuid():N}.tmp";
                File.WriteAllBytes(tempPath, encrypted);
                File.Move(tempPath, _filePath, overwrite: true);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            if (File.Exists(_filePath))
            {
                File.Delete(_filePath);
            }
        }
    }
}
