using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Switchly.Application.Vault;
using Switchly.Domain.Profiles;
using Switchly.Domain.Vault;
using Switchly.Infrastructure.Persistence;
using Switchly.Infrastructure.Security;
using Xunit;

namespace Switchly.Infrastructure.Tests;

public sealed class PasswordVaultIntegrationTests : IDisposable
{
    private readonly string _tempRoot;

    public PasswordVaultIntegrationTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), $"switchly-vault-tests-{Guid.NewGuid():N}");
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
    public async Task Vault_EncryptsWithUniqueNonceAndAad_IsolatesProfiles_AndNeverStoresPlaintext()
    {
        var dataDir = Path.Combine(_tempRoot, "Data");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "switchly-next.db");

        var opts = new DbContextOptionsBuilder<SwitchlyDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=False")
            .Options;

        var profileA = Guid.NewGuid();
        var profileB = Guid.NewGuid();

        await using (var db = new SwitchlyDbContext(opts))
        {
            await db.Database.MigrateAsync();
            db.Profiles.AddRange(
                new BrowserProfile
                {
                    ProfileId = profileA,
                    Name = "Profile A",
                    StorageRelativePath = profileA.ToString("D"),
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                },
                new BrowserProfile
                {
                    ProfileId = profileB,
                    Name = "Profile B",
                    StorageRelativePath = profileB.ToString("D"),
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                });
            await db.SaveChangesAsync();

            var vault = new PasswordVaultService(
                new EfVaultCredentialRepository(db),
                new WindowsDpapiProtector(),
                dataDir);

            const string secretPassword = "UltraSecretFlowPass!#2026";
            await vault.UpsertCredentialAsync(profileA, "https://flow.google/studio?x=1", "alice@example.com", secretPassword);

            // Verify raw SQLite DB file contains NO plaintext password or username (TEST-010, TEST-022)
            var rawDbBytes = File.ReadAllBytes(dbPath);
            var rawDbText = Encoding.UTF8.GetString(rawDbBytes);
            Assert.DoesNotContain(secretPassword, rawDbText, StringComparison.Ordinal);
            Assert.DoesNotContain("alice@example.com", rawDbText, StringComparison.Ordinal);

            // Exact origin match for Profile A; zero matches for Profile B or different origin/scheme
            var matchesA = await vault.FindMatchesForUrlAsync(profileA, "https://flow.google/projects/42?tab=edit");
            Assert.Single(matchesA);
            Assert.Equal("alice@example.com", matchesA[0].Username);
            Assert.Equal(secretPassword, matchesA[0].Password);

            Assert.Empty(await vault.FindMatchesForUrlAsync(profileB, "https://flow.google/"));
            Assert.Empty(await vault.FindMatchesForUrlAsync(profileA, "https://evil-flow.google/"));
            Assert.Empty(await vault.FindMatchesForUrlAsync(profileA, "http://flow.google/"));

            // Updating rotates to a NEW unique 96-bit CSPRNG nonce
            var beforeEntity = await db.VaultCredentials.AsNoTracking().SingleAsync();
            var originalNonce = (byte[])beforeEntity.Nonce.Clone();
            Assert.Equal(12, originalNonce.Length);
            Assert.Equal(16, beforeEntity.AuthenticationTag.Length);

            await vault.UpsertCredentialAsync(profileA, "https://flow.google/", "alice@example.com", "RotatedPassword!99");
            var afterEntity = await db.VaultCredentials.AsNoTracking().SingleAsync();
            Assert.False(originalNonce.SequenceEqual(afterEntity.Nonce));

            // AAD tamper test: mutating Origin directly in SQLite must fail AEAD authentication!
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE VaultCredentials SET Origin = 'https://evil.example' WHERE CredentialId = {0}",
                afterEntity.CredentialId);
            db.ChangeTracker.Clear();

            await Assert.ThrowsAsync<CryptographicException>(() =>
                vault.FindMatchesForUrlAsync(profileA, "https://evil.example/"));

            // Deleting Profile A cascades and removes its VaultCredentials (ON DELETE CASCADE)
            var pA = await db.Profiles.FindAsync(profileA);
            db.Profiles.Remove(pA!);
            await db.SaveChangesAsync();
            Assert.Empty(await db.VaultCredentials.ToListAsync());
        }
    }

    [Fact]
    public async Task CsvImport_ParsesQuotedChromeEdgeCsv_RejectsMalformedAndUnsafeRows_WithoutLeakingSecrets()
    {
        var dataDir = Path.Combine(_tempRoot, "CsvData");
        Directory.CreateDirectory(dataDir);
        var dbPath = Path.Combine(dataDir, "switchly-next.db");

        var opts = new DbContextOptionsBuilder<SwitchlyDbContext>()
            .UseSqlite($"Data Source={dbPath};Pooling=False")
            .Options;

        var profileId = Guid.NewGuid();
        await using var db = new SwitchlyDbContext(opts);
        await db.Database.MigrateAsync();
        db.Profiles.Add(new BrowserProfile
        {
            ProfileId = profileId,
            Name = "Import Target",
            StorageRelativePath = profileId.ToString("D"),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var vault = new PasswordVaultService(
            new EfVaultCredentialRepository(db),
            new WindowsDpapiProtector(),
            dataDir);

        var csvPath = Path.Combine(_tempRoot, "chrome-passwords.csv");
        const string quotedPass = "Pass,With\"Quote!123";
        const string badRowSecret = "DO_NOT_LEAK_BAD_ROW_SECRET";
        File.WriteAllText(
            csvPath,
            "\uFEFFname,url,username,password,note\r\n" +
            $"ChatGPT,https://chatgpt.com/auth/login,user1@example.com,\"Pass,With\"\"Quote!123\",work\r\n" +
            $"SeaArt,https://seaart.ai/,user2@example.com,SeaArtPass!456,\r\n" +
            $"UnsafeJS,javascript:alert(1),hacker,{badRowSecret},\r\n" +
            $"EmptyPass,https://dola.com/,user3@example.com,,\r\n" +
            $"MalformedShortRow,only-two-cols\r\n",
            Encoding.UTF8);

        var result = await vault.ImportChromeEdgeCsvAsync(profileId, csvPath);
        Assert.Equal(2, result.ImportedCount);
        Assert.Equal(0, result.UpdatedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal(2, result.MalformedRowCount);

        // Redacted warnings must NEVER leak row secrets or passwords (TEST-010, TEST-022)
        var allWarnings = string.Join(" ", result.RedactedWarnings);
        Assert.DoesNotContain(badRowSecret, allWarnings, StringComparison.Ordinal);
        Assert.DoesNotContain(quotedPass, allWarnings, StringComparison.Ordinal);

        // Verify quoted password with comma & escaped quote decrypted accurately
        var chatGptMatch = await vault.FindMatchesForUrlAsync(profileId, "https://chatgpt.com/");
        Assert.Single(chatGptMatch);
        Assert.Equal("user1@example.com", chatGptMatch[0].Username);
        Assert.Equal(quotedPass, chatGptMatch[0].Password);

        // Re-importing same CSV updates existing records instead of duplicating
        var reimport = await vault.ImportChromeEdgeCsvAsync(profileId, csvPath);
        Assert.Equal(0, reimport.ImportedCount);
        Assert.Equal(2, reimport.UpdatedCount);

        // Invalid CSV header rejected cleanly
        var badHeaderCsv = Path.Combine(_tempRoot, "bad-header.csv");
        File.WriteAllText(badHeaderCsv, "foo,bar,baz\r\n1,2,3\r\n");
        await Assert.ThrowsAsync<InvalidDataException>(() =>
            vault.ImportChromeEdgeCsvAsync(profileId, badHeaderCsv));
    }
}
