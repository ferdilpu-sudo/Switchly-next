namespace Switchly.Domain.Security;

/// <summary>
/// Format constants, cryptographic parameters, and safety bounds for portable backups (ADR-006, D3).
/// </summary>
public static class BackupLimits
{
    public const int CurrentArchiveVersion = 1;
    public const int ChunkSizeBytes = 64 * 1024;
    public const int Pbkdf2Iterations = 600_000;
    public const int MinPassphraseLength = 8;
    public const int MaxManifestBytes = 1024 * 1024;
    public const int MaxArchiveEntries = 50_000;
    public const long MaxSingleEntryBytes = 512L * 1024 * 1024;
    public const long MaxTotalExtractBytes = 4L * 1024 * 1024 * 1024;

    public static ReadOnlySpan<byte> ArchiveMagic => "SNBAK01\0"u8;

    /// <summary>Regenerable Chromium/CEF cache folders excluded from backups (ADR-006, D3).</summary>
    public static readonly IReadOnlySet<string> ExcludedCacheDirectoryNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Cache",
        "Code Cache",
        "GPUCache",
        "GrShaderCache",
        "DawnCache",
        "Crashpad",
        "ShaderCache",
        "CacheStorage",
    };

    /// <summary>Device-bound Chromium cookie/DPAPI state files excluded on backup & cross-device restore (D3).</summary>
    public static readonly IReadOnlySet<string> ExcludedDeviceBoundFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "Cookies",
        "Cookies-journal",
        "Local State",
    };
}
