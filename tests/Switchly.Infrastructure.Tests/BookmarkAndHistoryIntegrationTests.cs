using Microsoft.EntityFrameworkCore;
using Switchly.Application.Browser;
using Switchly.Application.Profiles;
using Switchly.Infrastructure.Persistence;

namespace Switchly.Infrastructure.Tests;

public class BookmarkAndHistoryIntegrationTests
{
    [Fact]
    public async Task BookmarksAndHistory_AreStrictlyIsolatedPerProfile_AndCascadeOnProfileDelete()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"switchly-bmhist-{Guid.NewGuid():N}");
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
            var bookmarkService = new BookmarkService(new EfBookmarkRepository(db));
            var historyService = new HistoryService(new EfHistoryRepository(db));

            var profileA = await profileService.CreateAsync(new CreateProfileCommand("Profile A"));
            var profileB = await profileService.CreateAsync(new CreateProfileCommand("Profile B"));

            // Bookmarks isolated per profile + folder support (TEST-007)
            var folderA = await bookmarkService.AddFolderAsync(profileA.ProfileId, "AI Work");
            var bmA = await bookmarkService.AddBookmarkAsync(profileA.ProfileId, "ChatGPT", "https://chatgpt.com", folderA.BookmarkId);
            var bmB = await bookmarkService.AddBookmarkAsync(profileB.ProfileId, "SeaArt", "https://seaart.ai");

            Assert.Equal(2, (await bookmarkService.ListAsync(profileA.ProfileId)).Count);
            Assert.Single(await bookmarkService.ListAsync(profileB.ProfileId));
            Assert.Single(await bookmarkService.ListAsync(profileA.ProfileId, searchText: "chat"));
            Assert.Empty(await bookmarkService.ListAsync(profileB.ProfileId, searchText: "chat"));

            // Profile B cannot delete Profile A's bookmark
            await bookmarkService.DeleteAsync(profileB.ProfileId, bmA.BookmarkId);
            Assert.Equal(2, (await bookmarkService.ListAsync(profileA.ProfileId)).Count);

            // History isolated per profile, strips query/credentials, increments visit count
            await historyService.RecordVisitAsync(profileA.ProfileId, "https://user:pw@flow.google/studio?token=secret#hash", "Google Flow");
            var secondVisit = await historyService.RecordVisitAsync(profileA.ProfileId, "https://flow.google/studio?token=other", "Google Flow");
            await historyService.RecordVisitAsync(profileB.ProfileId, "https://dola.com/chat", "Dola");

            Assert.Equal("https://flow.google/studio", secondVisit.Url);
            Assert.Equal(2, secondVisit.VisitCount);
            Assert.Single(await historyService.ListAsync(profileA.ProfileId));
            Assert.Single(await historyService.ListAsync(profileB.ProfileId));

            // Clearing Profile A history leaves Profile B intact
            await historyService.ClearProfileHistoryAsync(profileA.ProfileId);
            Assert.Empty(await historyService.ListAsync(profileA.ProfileId));
            Assert.Single(await historyService.ListAsync(profileB.ProfileId));

            // Deleting Profile B cascades its bookmarks & history
            await profileService.DeleteAsync(profileB.ProfileId);
            Assert.Empty(await bookmarkService.ListAsync(profileB.ProfileId));
            Assert.Empty(await historyService.ListAsync(profileB.ProfileId));
            Assert.NotNull(bmB);

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
