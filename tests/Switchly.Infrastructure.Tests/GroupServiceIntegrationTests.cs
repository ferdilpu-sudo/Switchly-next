using Microsoft.EntityFrameworkCore;
using Switchly.Application.Groups;
using Switchly.Application.Profiles;
using Switchly.Domain.Groups;
using Switchly.Infrastructure.Persistence;

namespace Switchly.Infrastructure.Tests;

public class GroupServiceIntegrationTests
{
    [Fact]
    public async Task ManualAndWebsiteDomainGroups_MeetTest005Contract()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"switchly-groups-{Guid.NewGuid():N}");
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
            var groupRepo = new EfGroupRepository(db);
            var storage = new LocalProfileStorageDirectory(profilesRoot, dataRoot);
            var profileService = new ProfileService(profileRepo, storage);
            var groupService = new GroupService(groupRepo, profileRepo);

            var profileA = await profileService.CreateAsync(new CreateProfileCommand("Profile A"));
            var profileB = await profileService.CreateAsync(new CreateProfileCommand("Profile B"));

            // Create and assign manual group
            var manual = await groupService.CreateManualGroupAsync("  Work Accounts  ");
            Assert.Equal("Work Accounts", manual.Name);
            Assert.Equal(GroupKind.Manual, manual.Kind);

            await groupService.AssignProfileToManualGroupAsync(profileA.ProfileId, manual.GroupId);
            await groupService.AssignProfileToManualGroupAsync(profileA.ProfileId, manual.GroupId); // idempotent
            Assert.Single(await groupService.GetProfileIdsInGroupAsync(manual.GroupId));

            var renamed = await groupService.RenameManualGroupAsync(manual.GroupId, "Client Work");
            Assert.Equal("Client Work", renamed.Name);

            // Auto WebsiteDomain grouping unifies www.seaart.ai and seaart.ai without altering manual groups (TEST-005)
            var auto1 = await groupService.SyncWebsiteDomainGroupAsync(profileA.ProfileId, "https://www.seaart.ai/create");
            var auto2 = await groupService.SyncWebsiteDomainGroupAsync(profileB.ProfileId, "https://seaart.ai/");
            Assert.Equal(auto1.GroupId, auto2.GroupId);
            Assert.Equal(GroupKind.WebsiteDomain, auto1.Kind);
            Assert.Equal("seaart.ai", auto1.DomainKey);
            Assert.Equal(2, (await groupService.GetProfileIdsInGroupAsync(auto1.GroupId)).Count);

            // System-derived WebsiteDomain groups are read-only for rename/delete/manual-assign
            await Assert.ThrowsAsync<SystemGroupReadOnlyException>(() => groupService.RenameManualGroupAsync(auto1.GroupId, "Custom"));
            await Assert.ThrowsAsync<SystemGroupReadOnlyException>(() => groupService.DeleteManualGroupAsync(auto1.GroupId));
            await Assert.ThrowsAsync<SystemGroupReadOnlyException>(() => groupService.AssignProfileToManualGroupAsync(profileA.ProfileId, auto1.GroupId));

            // Users may hide/unhide WebsiteDomain groups via IsHidden (D4)
            await groupService.SetGroupHiddenAsync(auto1.GroupId, true);
            Assert.DoesNotContain(await groupService.ListGroupsAsync(includeHidden: false), g => g.GroupId == auto1.GroupId);
            Assert.Contains(await groupService.ListGroupsAsync(includeHidden: true), g => g.GroupId == auto1.GroupId);

            // Deleting manual group removes membership without deleting profiles (schema.md §3)
            await groupService.DeleteManualGroupAsync(manual.GroupId);
            Assert.NotNull(await profileRepo.GetByIdAsync(profileA.ProfileId));
            Assert.Empty(await groupService.GetGroupsForProfileAsync(profileA.ProfileId, includeHidden: false));
            Assert.Single(await groupService.GetGroupsForProfileAsync(profileA.ProfileId, includeHidden: true));

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
