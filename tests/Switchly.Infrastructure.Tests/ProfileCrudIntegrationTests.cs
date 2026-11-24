using Microsoft.EntityFrameworkCore;
using Switchly.Application.Profiles;
using Switchly.Infrastructure.Persistence;

namespace Switchly.Infrastructure.Tests;

public class ProfileCrudIntegrationTests
{
    [Fact]
    public async Task ProfileService_WithRealSqliteAndStorage_IsolatesDuplicateAndDeletesCleanly()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"switchly-crud-{Guid.NewGuid():N}");
        var profilesRoot = Path.Combine(tempRoot, "Profiles");
        var dbPath = Path.Combine(tempRoot, "switchly-test.db");
        Directory.CreateDirectory(profilesRoot);

        var options = new DbContextOptionsBuilder<SwitchlyDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        try
        {
            using var db = new SwitchlyDbContext(options);
            db.Database.Migrate();

            var repo = new EfProfileRepository(db);
            var storage = new LocalProfileStorageDirectory(profilesRoot);
            var service = new ProfileService(repo, storage);

            var source = await service.CreateAsync(new CreateProfileCommand(
                Name: "Source Profile",
                StartupUrl: "https://dola.com",
                ColorHex: "#2288FF"));

            var sourceDir = storage.ResolveDirectChildPath(source.StorageRelativePath);
            Assert.True(Directory.Exists(sourceDir));

            // Write a synthetic cookie marker file to prove duplicate does NOT copy storage.
            var syntheticCookieFile = Path.Combine(sourceDir, "Cookies");
            await File.WriteAllTextAsync(syntheticCookieFile, "synthetic-session-marker");

            var copy = await service.DuplicateConfigurationAsync(new DuplicateProfileCommand(source.ProfileId, "Copy Profile"));
            var copyDir = storage.ResolveDirectChildPath(copy.StorageRelativePath);

            Assert.True(Directory.Exists(copyDir));
            Assert.NotEqual(sourceDir, copyDir);
            Assert.False(File.Exists(Path.Combine(copyDir, "Cookies")));
            Assert.Empty(Directory.GetFiles(copyDir));

            await service.DeleteAsync(copy.ProfileId);
            Assert.False(Directory.Exists(copyDir));
            Assert.True(Directory.Exists(sourceDir));

            db.Database.EnsureDeleted();
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("nested/child")]
    [InlineData(@"nested\child")]
    [InlineData(@"C:\Windows\System32")]
    public void LocalProfileStorageDirectory_RejectsTraversalAndNestedPaths(string unsafePath)
    {
        var root = Path.Combine(Path.GetTempPath(), $"switchly-path-{Guid.NewGuid():N}");
        var storage = new LocalProfileStorageDirectory(root);

        Assert.Throws<ArgumentException>(() => storage.ResolveDirectChildPath(unsafePath));
    }
}
