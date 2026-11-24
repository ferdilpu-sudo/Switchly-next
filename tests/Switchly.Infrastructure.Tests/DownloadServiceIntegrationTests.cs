using Microsoft.EntityFrameworkCore;
using Switchly.Application.Browser;
using Switchly.Application.Profiles;
using Switchly.Domain.Downloads;
using Switchly.Infrastructure.Persistence;

namespace Switchly.Infrastructure.Tests;

public class DownloadServiceIntegrationTests
{
    [Fact]
    public async Task Downloads_AreIsolatedPerProfile_ReconcileStaleInProgressOnStartup_AndCascadeDelete()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"switchly-dl-{Guid.NewGuid():N}");
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
            var downloadService = new DownloadService(new EfDownloadRepository(db));

            var profileA = await profileService.CreateAsync(new CreateProfileCommand("Profile A"));
            var profileB = await profileService.CreateAsync(new CreateProfileCommand("Profile B"));

            // Start & complete a download for Profile A
            var targetA = Path.Combine(tempRoot, "A", "image.png");
            await downloadService.RecordOrUpdateAsync(new DownloadUpdateEvent(
                profileA.ProfileId, "../../image.png", targetA, "image/png", 512, 1024, DownloadState.InProgress));
            var completedA = await downloadService.RecordOrUpdateAsync(new DownloadUpdateEvent(
                profileA.ProfileId, "image.png", targetA, "image/png", 1024, 1024, DownloadState.Completed));

            Assert.Equal("image.png", completedA.SuggestedFileName);
            Assert.Equal(DownloadState.Completed, completedA.State);
            Assert.NotNull(completedA.CompletedAtUtc);

            // Start an unfinished download for Profile B
            var targetB = Path.Combine(tempRoot, "B", "video.mp4");
            await downloadService.RecordOrUpdateAsync(new DownloadUpdateEvent(
                profileB.ProfileId, "CON.mp4", targetB, "video/mp4", 100, 5000, DownloadState.InProgress));

            Assert.Single(await downloadService.ListByProfileAsync(profileA.ProfileId));
            Assert.Single(await downloadService.ListByProfileAsync(profileB.ProfileId));

            // Crash-recovery on startup transitions stale InProgress to Failed without touching Completed
            await downloadService.MarkInterruptedOnStartupAsync();
            var listA = await downloadService.ListByProfileAsync(profileA.ProfileId);
            var listB = await downloadService.ListByProfileAsync(profileB.ProfileId);
            Assert.Equal(DownloadState.Completed, listA[0].State);
            Assert.Equal(DownloadState.Failed, listB[0].State);
            Assert.Equal("_CON.mp4", listB[0].SuggestedFileName);
            Assert.NotNull(listB[0].CompletedAtUtc);

            // Deleting Profile A cascades its download records
            await profileService.DeleteAsync(profileA.ProfileId);
            Assert.Empty(await downloadService.ListByProfileAsync(profileA.ProfileId));
            Assert.Single(await downloadService.ListByProfileAsync(profileB.ProfileId));

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
