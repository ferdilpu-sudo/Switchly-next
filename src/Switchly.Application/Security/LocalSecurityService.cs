using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Switchly.Domain.Security;

namespace Switchly.Application.Security;

/// <summary>
/// Persists DPAPI-protected local unlock configuration without ever storing plaintext passwords (TASK-029, ADR-005).
/// Fails closed if the DPAPI blob cannot be decrypted (e.g. different Windows account, TEST-011).
/// </summary>
public sealed class LocalSecurityService : ILocalSecurityService
{
    private static readonly byte[] ConfigEntropy = "SwitchlyNext.LocalSecurity.v1"u8.ToArray();

    private readonly ILocalDataProtector _protector;
    private readonly string _configFilePath;

    public LocalSecurityService(ILocalDataProtector protector, string dataDirectory)
    {
        _protector = protector ?? throw new ArgumentNullException(nameof(protector));
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            throw new ArgumentException("Data directory is required.", nameof(dataDirectory));
        }

        Directory.CreateDirectory(dataDirectory);
        _configFilePath = Path.Combine(dataDirectory, "local-security.dat");
    }

    public LocalSecurityStatus GetStatus()
    {
        if (!File.Exists(_configFilePath))
        {
            return new LocalSecurityStatus(
                IsProtectionHealthy: true,
                IsMasterPasswordEnabled: false,
                IsWindowsHelloAvailable: false);
        }

        try
        {
            var config = ReadProtectedConfig();
            return new LocalSecurityStatus(
                IsProtectionHealthy: true,
                IsMasterPasswordEnabled: config.IsMasterPasswordEnabled,
                IsWindowsHelloAvailable: false);
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or IOException)
        {
            // Fail-closed: never silently disable lock when DPAPI decryption fails (TEST-011).
            return new LocalSecurityStatus(
                IsProtectionHealthy: false,
                IsMasterPasswordEnabled: true,
                IsWindowsHelloAvailable: false,
                ErrorMessage: "Konfigurasi keamanan lokal tidak dapat dibuka oleh akun Windows ini (DPAPI fail-closed).");
        }
    }

    public void EnableMasterPassword(string masterPassword)
    {
        if (string.IsNullOrWhiteSpace(masterPassword) || masterPassword.Length < BackupLimits.MinPassphraseLength)
        {
            throw new ArgumentException(
                $"Kata sandi utama minimal {BackupLimits.MinPassphraseLength} karakter.",
                nameof(masterPassword));
        }

        var salt = RandomNumberGenerator.GetBytes(32);
        var hash = DeriveVerifier(masterPassword, salt, BackupLimits.Pbkdf2Iterations);

        var config = new StoredSecurityConfig(
            Version: 1,
            IsMasterPasswordEnabled: true,
            KdfIterations: BackupLimits.Pbkdf2Iterations,
            SaltBase64: Convert.ToBase64String(salt),
            VerifierHashBase64: Convert.ToBase64String(hash));

        WriteProtectedConfig(config);
    }

    public bool VerifyMasterPassword(string candidatePassword)
    {
        if (string.IsNullOrEmpty(candidatePassword))
        {
            return false;
        }

        if (!File.Exists(_configFilePath))
        {
            return true;
        }

        try
        {
            var config = ReadProtectedConfig();
            if (!config.IsMasterPasswordEnabled)
            {
                return true;
            }

            if (string.IsNullOrEmpty(config.SaltBase64) || string.IsNullOrEmpty(config.VerifierHashBase64))
            {
                return false;
            }

            var salt = Convert.FromBase64String(config.SaltBase64);
            var expectedHash = Convert.FromBase64String(config.VerifierHashBase64);
            var actualHash = DeriveVerifier(candidatePassword, salt, config.KdfIterations);

            try
            {
                return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(actualHash);
            }
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException or IOException)
        {
            // Fail closed; never leak exception internals or secret material.
            return false;
        }
    }

    public bool DisableMasterPassword(string currentMasterPassword)
    {
        if (!VerifyMasterPassword(currentMasterPassword))
        {
            return false;
        }

        var config = new StoredSecurityConfig(
            Version: 1,
            IsMasterPasswordEnabled: false,
            KdfIterations: 0,
            SaltBase64: string.Empty,
            VerifierHashBase64: string.Empty);

        WriteProtectedConfig(config);
        return true;
    }

    private StoredSecurityConfig ReadProtectedConfig()
    {
        var encrypted = File.ReadAllBytes(_configFilePath);
        var plaintext = _protector.Unprotect(encrypted, ConfigEntropy);
        try
        {
            return JsonSerializer.Deserialize<StoredSecurityConfig>(plaintext)
                ?? throw new CryptographicException("Invalid security configuration payload.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private void WriteProtectedConfig(StoredSecurityConfig config)
    {
        var plaintext = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(config));
        try
        {
            var encrypted = _protector.Protect(plaintext, ConfigEntropy);
            var tempPath = _configFilePath + ".tmp";
            File.WriteAllBytes(tempPath, encrypted);
            File.Move(tempPath, _configFilePath, overwrite: true);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }

    private static byte[] DeriveVerifier(string password, byte[] salt, int iterations)
    {
        var passwordBytes = Encoding.UTF8.GetBytes(password);
        try
        {
            return Rfc2898DeriveBytes.Pbkdf2(passwordBytes, salt, iterations, HashAlgorithmName.SHA256, 32);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(passwordBytes);
        }
    }

    private sealed record StoredSecurityConfig(
        int Version,
        bool IsMasterPasswordEnabled,
        int KdfIterations,
        string SaltBase64,
        string VerifierHashBase64);
}
