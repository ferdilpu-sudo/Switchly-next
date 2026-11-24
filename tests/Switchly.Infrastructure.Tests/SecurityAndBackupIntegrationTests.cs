using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Switchly.Application.Security;
using Switchly.Domain.Profiles;
using Switchly.Domain.Security;
using Switchly.Infrastructure.Persistence;
using Switchly.Infrastructure.Security;
using Xunit;

namespace Switchly.Infrastructure.Tests;

public sealed class SecurityAndBackupIntegrationTests : IDisposable
{
    private readonly string _tempRoot;

    public SecurityAndBackupIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"switchly-m007-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempRoot);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }
        catch (IOException)
        {
        }
    }

    [Fact]
    public void LocalSecurity_DpapiMasterPassword_RoundTrips_AndFailsClosedWhenUnreadable()
    {
        var dataDir = Path.Combine(_tempRoot, "LocalSec");
        var protector = new WindowsDpapiProtector();
        var service = new LocalSecurityService(protector, dataDir);

        Assert.True(service.GetStatus().IsProtectionHealthy);
        Assert.False(service.GetStatus().IsMasterPasswordEnabled);

        const string secretPass = "SuperSecretMaster!2026";
        service.EnableMasterPassword(secretPass);

        Assert.True(service.GetStatus().IsMasterPasswordEnabled);
        Assert.True(service.VerifyMasterPassword(secretPass));
        Assert.False(service.VerifyMasterPassword("WrongPassword!2026"));

        // Verify plaintext password never appears on disk (TEST-022)
        var rawBytes = File.ReadAllBytes(Path.Combine(dataDir, "local-security.dat"));
        Assert.DoesNotContain(secretPass, Encoding.UTF8.GetString(rawBytes), StringComparison.Ordinal);

        // Simulate DPAPI unavailable under another Windows user account -> must fail closed (TEST-011)
        var otherUserService = new LocalSecurityService(new AlwaysFailingProtector(), dataDir);
        var failClosedStatus = otherUserService.GetStatus();
        Assert.False(failClosedStatus.IsProtectionHealthy);
        Assert.True(failClosedStatus.IsMasterPasswordEnabled);
        Assert.False(otherUserService.VerifyMasterPassword(secretPass));

        // Disable requires correct current password
        Assert.False(service.DisableMasterPassword("WrongPassword!2026"));
        Assert.True(service.DisableMasterPassword(secretPass));
        Assert.False(service.GetStatus().IsMasterPasswordEnabled);
    }

    [Fact]
    public async Task PortableBackupAndRestore_EndToEnd_ExcludesCachesAndCookies_AndSwapsOnRestart()
    {
        var sourceAppData = Path.Combine(_tempRoot, "DeviceA");
        var sourceDataDir = Path.Combine(sourceAppData, "Data");
        var sourceProfilesDir = Path.Combine(sourceAppData, "Profiles");
        Directory.CreateDirectory(sourceDataDir);
        Directory.CreateDirectory(sourceProfilesDir);

        var sourceDbPath = Path.Combine(sourceDataDir, "switchly-next.db");
        var profileId = Guid.NewGuid();
        var relPath = profileId.ToString("D");

        // Seed live DB on Device A
        var options = new DbContextOptionsBuilder<SwitchlyDbContext>()
            .UseSqlite($"Data Source={sourceDbPath};Pooling=False")
            .Options;

        await using (var db = new SwitchlyDbContext(options))
        {
            await db.Database.MigrateAsync();
            db.Profiles.Add(new BrowserProfile
            {
                ProfileId = profileId,
                Name = "Google Flow Account",
                StorageRelativePath = relPath,
                StartupUrl = "https://flow.google",
                IsFavorite = true,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();

            // Seed profile directory with portable data + excluded cache + excluded device-bound cookies + CEF global Local State
            var profileDir = Path.Combine(sourceProfilesDir, relPath);
            Directory.CreateDirectory(Path.Combine(profileDir, "Cache"));
            Directory.CreateDirectory(Path.Combine(profileDir, "Network"));
            File.WriteAllText(Path.Combine(profileDir, "Preferences"), "{\"theme\":\"dark\"}");
            File.WriteAllText(Path.Combine(profileDir, "Cache", "disposable.bin"), "CACHE_JUNK");
            File.WriteAllText(Path.Combine(profileDir, "Network", "Cookies"), "DEVICE_BOUND_COOKIE");
            File.WriteAllText(Path.Combine(sourceProfilesDir, "Local State"), "CEF_GLOBAL_STATE");

            var archivePath = Path.Combine(_tempRoot, "device-a.snbak");
            const string passphrase = "CorrectBackupPassphrase!99";

            var backupService = new PortableBackupRestoreService(
                sourceAppData,
                new EfProfileRepository(db),
                new EfBackupJobRepository(db),
                kdfIterationsOverride: 10_000);

            var created = await backupService.CreateBackupAsync(archivePath, passphrase);
            Assert.Equal(1, created.ProfileCount);
            Assert.True(File.Exists(archivePath));

            // Verify BackupJob audit record does not contain passphrase (TEST-022)
            var jobs = await new EfBackupJobRepository(db).ListRecentAsync();
            Assert.Single(jobs);
            Assert.Equal(BackupJobStatus.Completed, jobs[0].Status);
            Assert.DoesNotContain(passphrase, jobs[0].ResultSummaryJson, StringComparison.Ordinal);

            // Inspect header on Device B without decrypting
            var deviceBAppData = Path.Combine(_tempRoot, "DeviceB");
            Directory.CreateDirectory(Path.Combine(deviceBAppData, "Data"));
            var deviceBService = new PortableBackupRestoreService(
                deviceBAppData,
                new EfProfileRepository(db),
                new EfBackupJobRepository(db),
                kdfIterationsOverride: 10_000);

            var manifest = deviceBService.InspectBackup(archivePath);
            Assert.Equal(BackupLimits.CurrentArchiveVersion, manifest.ArchiveVersion);
            Assert.Equal(1, manifest.ProfileCount);
            Assert.True(manifest.ExcludesDeviceBoundCookies);

            // Stage restore on Device B
            var staged = await deviceBService.StageRestoreAsync(archivePath, passphrase);
            Assert.True(staged.ExtractedFileCount >= 2);
            Assert.True(File.Exists(PendingRestoreApplier.GetMarkerPath(deviceBAppData)));

            // Apply restart-to-restore staging swap BEFORE Cef.Initialize()/EF Core
            var applied = PendingRestoreApplier.ApplyPendingRestoreIfAny(deviceBAppData);
            Assert.True(applied);
            Assert.False(File.Exists(PendingRestoreApplier.GetMarkerPath(deviceBAppData)));

            // Verify restored profile data, excluded cache, excluded cookies, and excluded CEF global state (D3, TEST-012)
            var restoredProfileDir = Path.Combine(deviceBAppData, "Profiles", relPath);
            Assert.True(File.Exists(Path.Combine(restoredProfileDir, "Preferences")));
            Assert.False(File.Exists(Path.Combine(restoredProfileDir, "Cache", "disposable.bin")));
            Assert.False(File.Exists(Path.Combine(restoredProfileDir, "Network", "Cookies")));
            Assert.False(File.Exists(Path.Combine(deviceBAppData, "Profiles", "Local State")));

            // Verify restored SQLite DB has the profile
            var deviceBDbPath = Path.Combine(deviceBAppData, "Data", "switchly-next.db");
            var deviceBOptions = new DbContextOptionsBuilder<SwitchlyDbContext>()
                .UseSqlite($"Data Source={deviceBDbPath};Pooling=False")
                .Options;
            await using var restoredDb = new SwitchlyDbContext(deviceBOptions);
            var restoredProfile = await restoredDb.Profiles.SingleAsync();
            Assert.Equal("Google Flow Account", restoredProfile.Name);
            Assert.True(restoredProfile.IsFavorite);
        }
    }

    [Fact]
    public async Task ChunkedAesGcm_RejectsWrongPassphrase_Tamper_Truncation_Trailing_FutureVersion_AndPathTraversal()
    {
        // Multi-chunk plaintext (> 150 KiB so >= 3 chunks of 64 KiB)
        var plaintext = RandomNumberGenerator.GetBytes(160 * 1024);
        var manifest = new BackupManifest(
            BackupLimits.CurrentArchiveVersion,
            DateTimeOffset.UtcNow,
            1,
            [Guid.NewGuid()],
            plaintext.Length,
            true);

        using var encryptedStream = new MemoryStream();
        ChunkedAesGcmArchiveCrypto.EncryptStream(
            new MemoryStream(plaintext),
            encryptedStream,
            manifest,
            "ValidPassphrase!123",
            kdfIterationsOverride: 10_000);
        var validArchive = encryptedStream.ToArray();

        // 1. Valid decrypt succeeds
        using (var decOut = new MemoryStream())
        {
            ChunkedAesGcmArchiveCrypto.DecryptStream(new MemoryStream(validArchive), decOut, "ValidPassphrase!123");
            Assert.Equal(plaintext, decOut.ToArray());
        }

        // 2. Wrong passphrase rejected (TEST-013)
        Assert.Throws<CryptographicException>(() =>
            ChunkedAesGcmArchiveCrypto.DecryptStream(new MemoryStream(validArchive), new MemoryStream(), "WrongPassphrase!123"));

        // 3. Flipped ciphertext byte rejected (TEST-013)
        var tamperedCipher = (byte[])validArchive.Clone();
        tamperedCipher[^10] ^= 0xFF;
        Assert.Throws<CryptographicException>(() =>
            ChunkedAesGcmArchiveCrypto.DecryptStream(new MemoryStream(tamperedCipher), new MemoryStream(), "ValidPassphrase!123"));

        // 4. Flipped header manifest byte rejected via HeaderHash AAD binding (TEST-013)
        var tamperedHeader = (byte[])validArchive.Clone();
        tamperedHeader[70] ^= 0x01;
        Assert.Throws<CryptographicException>(() =>
            ChunkedAesGcmArchiveCrypto.DecryptStream(new MemoryStream(tamperedHeader), new MemoryStream(), "ValidPassphrase!123"));

        // 5. Truncated archive (missing final chunk) rejected (TEST-013)
        var truncated = validArchive[..(validArchive.Length - 20_000)];
        Assert.Throws<CryptographicException>(() =>
            ChunkedAesGcmArchiveCrypto.DecryptStream(new MemoryStream(truncated), new MemoryStream(), "ValidPassphrase!123"));

        // 6. Trailing bytes after final chunk rejected (TEST-013)
        var withTrailing = new byte[validArchive.Length + 4];
        validArchive.CopyTo(withTrailing, 0);
        Assert.Throws<CryptographicException>(() =>
            ChunkedAesGcmArchiveCrypto.DecryptStream(new MemoryStream(withTrailing), new MemoryStream(), "ValidPassphrase!123"));

        // 7. Unsupported future archive version rejected (schema.md §7)
        var futureVersion = (byte[])validArchive.Clone();
        BinaryPrimitives.WriteUInt32LittleEndian(futureVersion.AsSpan(8, 4), 99);
        Assert.Throws<NotSupportedException>(() =>
            ChunkedAesGcmArchiveCrypto.InspectHeader(new MemoryStream(futureVersion)));

        // 8. Malicious path traversal inside valid encrypted ZIP rejected & leaves staging clean (TEST-013)
        var maliciousZipBytes = CreateMaliciousTraversalZip();
        var maliciousArchivePath = Path.Combine(_tempRoot, "traversal.snbak");
        using (var fs = File.Create(maliciousArchivePath))
        {
            ChunkedAesGcmArchiveCrypto.EncryptStream(
                new MemoryStream(maliciousZipBytes),
                fs,
                manifest,
                "ValidPassphrase!123",
                kdfIterationsOverride: 10_000);
        }

        var targetAppData = Path.Combine(_tempRoot, "TraversalTarget");
        var dbPath = Path.Combine(targetAppData, "Data", "switchly-next.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        var opts = new DbContextOptionsBuilder<SwitchlyDbContext>().UseSqlite($"Data Source={dbPath};Pooling=False").Options;
        await using var db = new SwitchlyDbContext(opts);
        await db.Database.MigrateAsync();

        var service = new PortableBackupRestoreService(
            targetAppData,
            new EfProfileRepository(db),
            new EfBackupJobRepository(db),
            kdfIterationsOverride: 10_000);

        await Assert.ThrowsAsync<CryptographicException>(() =>
            service.StageRestoreAsync(maliciousArchivePath, "ValidPassphrase!123"));

        Assert.False(File.Exists(Path.Combine(targetAppData, "escaped.txt")));
        Assert.False(File.Exists(PendingRestoreApplier.GetMarkerPath(targetAppData)));
    }

    [Fact]
    public async Task BackupCancellation_CleansUpTempFiles_AndRecordsCancelledJob()
    {
        var appData = Path.Combine(_tempRoot, "CancelTest");
        var dataDir = Path.Combine(appData, "Data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "switchly-next.db");

        var opts = new DbContextOptionsBuilder<SwitchlyDbContext>().UseSqlite($"Data Source={dbPath};Pooling=False").Options;
        await using var db = new SwitchlyDbContext(opts);
        await db.Database.MigrateAsync();

        var service = new PortableBackupRestoreService(
            appData,
            new EfProfileRepository(db),
            new EfBackupJobRepository(db),
            kdfIterationsOverride: 10_000);

        var destPath = Path.Combine(_tempRoot, "cancelled.snbak");
        using var cts = new CancellationTokenSource();
        var progress = new SynchronousCancelOnSecondReportProgress(cts);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.CreateBackupAsync(destPath, "CancelPassphrase!1", progress, cts.Token));

        Assert.False(File.Exists(destPath));
        Assert.False(File.Exists(destPath + ".tmp"));

        var jobs = await new EfBackupJobRepository(db).ListRecentAsync();
        Assert.Single(jobs);
        Assert.Equal(BackupJobStatus.Cancelled, jobs[0].Status);
    }

    private static byte[] CreateMaliciousTraversalZip()
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = zip.CreateEntry("../escaped.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("pwned");
        }

        return ms.ToArray();
    }

    private sealed class AlwaysFailingProtector : ILocalDataProtector
    {
        public byte[] Protect(ReadOnlySpan<byte> plaintext, ReadOnlySpan<byte> entropy = default)
            => throw new CryptographicException("Simulated other-account DPAPI failure.");

        public byte[] Unprotect(ReadOnlySpan<byte> ciphertext, ReadOnlySpan<byte> entropy = default)
            => throw new CryptographicException("Simulated other-account DPAPI failure.");
    }

    private sealed class SynchronousCancelOnSecondReportProgress : IProgress<BackupProgress>
    {
        private readonly CancellationTokenSource _cts;
        private int _count;

        public SynchronousCancelOnSecondReportProgress(CancellationTokenSource cts)
        {
            _cts = cts;
        }

        public void Report(BackupProgress value)
        {
            if (++_count >= 2)
            {
                _cts.Cancel();
            }
        }
    }
}
