using Microsoft.EntityFrameworkCore;
using Switchly.Application.Browser;
using Switchly.Application.Profiles;
using Switchly.Infrastructure.Persistence;

namespace Switchly.Infrastructure.Tests;

public class TabSessionIntegrationTests
{
    [Fact]
    public async Task TabSessions_PersistOnlySafeUrlsPerProfile_RestoreInOrder_AndCascadeOnDelete()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"switchly-tabs-{Guid.NewGuid():N}");
        var profilesRoot = Path.Combine(tempRoot, "Profiles");
        var dataRoot = Path.Combine(tempRoot, "Data");
        var dbPath = Path.Combine(dataRoot, "switchly-test.db");
        Directory.CreateDirectory(profilesRoot);
        Directory.CreateDirectory(dataRoot);

        var options = new DbContextOptionsBuilder<SwitchlyDbContext>()
            .UseSqlite($"Data Source={dbPath}")
            .Options;

        try
        {
            using var db = new SwitchlyDbContext(options);
            db.Database.Migrate();

            var profileRepo = new EfProfileRepository(db);
            var storage = new LocalProfileStorageDirectory(profilesRoot, dataRoot);
            var profileService = new ProfileService(profileRepo, storage);
            var tabService = new TabSessionService(new EfTabSessionRepository(db));

            var profileA = await profileService.CreateAsync(new CreateProfileCommand("Profile A"));
            var profileB = await profileService.CreateAsync(new CreateProfileCommand("Profile B"));

            // Mix safe URLs, about:blank, credentials/query, and unsafe schemes (schema.md §3 & §5)
            await tabService.SaveOpenTabsAsync(
                profileA.ProfileId,
                Guid.NewGuid(),
                [
                    new TabSnapshotInput("https://user:pass@chatgpt.com/c/1?token=secret#frag", "ChatGPT"),
                    new TabSnapshotInput("javascript:alert(1)", "Evil JS"),
                    new TabSnapshotInput("file:///C:/Windows/win.ini", "Local File"),
                    new TabSnapshotInput("data:text/html,<h1>x</h1>", "Data"),
                    new TabSnapshotInput("about:blank", "Blank"),
                    new TabSnapshotInput("https://seaart.ai/", "SeaArt"),
                ]);

            await tabService.SaveOpenTabsAsync(
                profileB.ProfileId,
                Guid.NewGuid(),
                [
                    new TabSnapshotInput("https://dola.com/chat/", "Dola"),
                ]);

            var restoredA = await tabService.GetRestorableTabsAsync(profileA.ProfileId);
            var restoredB = await tabService.GetRestorableTabsAsync(profileB.ProfileId);

            Assert.Equal(
                ["https://chatgpt.com/c/1", "about:blank", "https://seaart.ai/"],
                restoredA.Select(t => t.Url));
            Assert.Equal([0, 1, 2], restoredA.Select(t => t.Position));
            Assert.Equal(["https://dola.com/chat/"], restoredB.Select(t => t.Url));

            // Deleting Profile A cascades its tab sessions without touching Profile B
            await profileService.DeleteAsync(profileA.ProfileId);
            Assert.Empty(await tabService.GetRestorableTabsAsync(profileA.ProfileId));
            Assert.Single(await tabService.GetRestorableTabsAsync(profileB.ProfileId));

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
}
