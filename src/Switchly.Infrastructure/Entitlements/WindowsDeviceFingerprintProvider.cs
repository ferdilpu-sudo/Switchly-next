using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using Switchly.Application.Entitlements;

namespace Switchly.Infrastructure.Entitlements;

/// <summary>
/// Produces a stable one-way SHA-256 device hash for Trial/License anti-abuse without exposing raw hardware IDs.
/// </summary>
public sealed class WindowsDeviceFingerprintProvider : IDeviceFingerprintProvider
{
    private const string AppSalt = "switchly-device-v1";
    private readonly string _fallbackSeedPath;
    private string? _cachedHash;

    public WindowsDeviceFingerprintProvider(string dataDirectory)
    {
        _fallbackSeedPath = Path.Combine(dataDirectory, "device-seed.bin");
    }

    public string GetDeviceHash()
    {
        if (_cachedHash is not null)
        {
            return _cachedHash;
        }

        var rawMachineId = TryReadWindowsMachineGuid() ?? GetOrCreateFallbackSeed();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{AppSalt}:{rawMachineId}"));
        _cachedHash = $"sha256:{Convert.ToHexString(hashBytes).ToLowerInvariant()}";
        return _cachedHash;
    }

    private static string? TryReadWindowsMachineGuid()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var key = hklm.OpenSubKey(@"SOFTWARE\Microsoft\Cryptography", writable: false);
            var guid = key?.GetValue("MachineGuid")?.ToString()?.Trim();
            return string.IsNullOrWhiteSpace(guid) ? null : guid;
        }
        catch
        {
            return null;
        }
    }

    private string GetOrCreateFallbackSeed()
    {
        if (File.Exists(_fallbackSeedPath))
        {
            return Convert.ToHexString(File.ReadAllBytes(_fallbackSeedPath));
        }

        var dir = Path.GetDirectoryName(_fallbackSeedPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var bytes = RandomNumberGenerator.GetBytes(32);
        File.WriteAllBytes(_fallbackSeedPath, bytes);
        return Convert.ToHexString(bytes);
    }
}
