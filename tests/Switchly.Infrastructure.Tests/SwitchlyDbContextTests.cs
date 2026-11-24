using Microsoft.EntityFrameworkCore;
using Switchly.Domain.Groups;
using Switchly.Domain.Profiles;
using Switchly.Infrastructure.Persistence;

namespace Switchly.Infrastructure.Tests;

public class SwitchlyDbContextTests
{
    [Fact]
    public void Migrate_And_AddAndReadProfile_DefaultsMatchSchemaContract()
    {
        var path = Path.Combine(Path.GetTempPath(), $"switchly-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<SwitchlyDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        using var db = new SwitchlyDbContext(options);
        try
        {
            db.Database.Migrate();

            var profileId = Guid.NewGuid();
            db.Profiles.Add(new BrowserProfile
            {
                ProfileId = profileId,
                Name = "Test Profile",
                StorageRelativePath = profileId.ToString("N"),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
            db.SaveChanges();

            var loaded = db.Profiles.Single(p => p.ProfileId == profileId);
            Assert.Equal("Test Profile", loaded.Name);
            Assert.False(loaded.IsFavorite);
            Assert.False(loaded.IsArchived);
            Assert.True(loaded.RestoreLastSession);
            Assert.True(loaded.PersistSessionCookies);
        }
        finally
        {
            db.Database.EnsureDeleted();
        }
    }

    [Fact]
    public void ProfileGroup_IsHidden_And_MembershipCascadeDelete_Work()
    {
        var path = Path.Combine(Path.GetTempPath(), $"switchly-test-{Guid.NewGuid():N}.db");
        var options = new DbContextOptionsBuilder<SwitchlyDbContext>()
            .UseSqlite($"Data Source={path}")
            .Options;

        using var db = new SwitchlyDbContext(options);
        try
        {
            db.Database.Migrate();

            var profileId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var now = DateTimeOffset.UtcNow;

            db.Profiles.Add(new BrowserProfile
            {
                ProfileId = profileId,
                Name = "Fav Profile",
                StorageRelativePath = profileId.ToString("N"),
                IsFavorite = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
            db.Groups.Add(new ProfileGroup
            {
                GroupId = groupId,
                Name = "chatgpt.com",
                Kind = GroupKind.WebsiteDomain,
                DomainKey = "chatgpt.com",
                IsHidden = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            });
            db.GroupMemberships.Add(new ProfileGroupMembership
            {
                ProfileId = profileId,
                GroupId = groupId,
                MembershipSource = MembershipSource.Auto,
            });
            db.SaveChanges();

            Assert.True(db.Profiles.Single(p => p.ProfileId == profileId).IsFavorite);
            Assert.True(db.Groups.Single(g => g.GroupId == groupId).IsHidden);
            Assert.Equal(1, db.GroupMemberships.Count());

            var profile = db.Profiles.Single(p => p.ProfileId == profileId);
            db.Profiles.Remove(profile);
            db.SaveChanges();

            Assert.Equal(0, db.GroupMemberships.Count());
            Assert.Equal(1, db.Groups.Count());
        }
        finally
        {
            db.Database.EnsureDeleted();
        }
    }
}
