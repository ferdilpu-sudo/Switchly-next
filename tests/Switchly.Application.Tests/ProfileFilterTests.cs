using Switchly.Application.Profiles;
using Switchly.Domain.Profiles;

namespace Switchly.Application.Tests;

public class ProfileFilterTests
{
    [Fact]
    public void Apply_FiltersBySearchFavoritesGroupAndArchive_WithoutMutatingSource()
    {
        var p1 = MakeProfile("Work ChatGPT", "https://chatgpt.com", isFavorite: true, isArchived: false);
        var p2 = MakeProfile("Personal SeaArt", "https://seaart.ai", isFavorite: false, isArchived: false);
        var p3 = MakeProfile("Old Flow", "https://flow.google", isFavorite: true, isArchived: true);
        var source = new List<BrowserProfile> { p1, p2, p3 };

        // Default excludes archived
        var active = ProfileFilter.Apply(source, new ProfileFilterCriteria());
        Assert.Equal([p1.ProfileId, p2.ProfileId], active.Select(p => p.ProfileId));

        // Favorites only
        var favs = ProfileFilter.Apply(source, new ProfileFilterCriteria(FavoritesOnly: true));
        Assert.Equal([p1.ProfileId], favs.Select(p => p.ProfileId));

        // Search by domain in StartupUrl (case-insensitive)
        var byDomain = ProfileFilter.Apply(source, new ProfileFilterCriteria(SearchText: "SEAART.AI"));
        Assert.Equal([p2.ProfileId], byDomain.Select(p => p.ProfileId));

        // Group filter combined with search
        var allowed = new HashSet<Guid> { p1.ProfileId, p2.ProfileId };
        var combined = ProfileFilter.Apply(source, new ProfileFilterCriteria(SearchText: "work", AllowedProfileIds: allowed));
        Assert.Equal([p1.ProfileId], combined.Select(p => p.ProfileId));

        // Archived only
        var archived = ProfileFilter.Apply(source, new ProfileFilterCriteria(ArchivedOnly: true));
        Assert.Equal([p3.ProfileId], archived.Select(p => p.ProfileId));

        // Source list order unchanged (AC-016)
        Assert.Equal([p1.ProfileId, p2.ProfileId, p3.ProfileId], source.Select(p => p.ProfileId));
    }

    private static BrowserProfile MakeProfile(string name, string url, bool isFavorite, bool isArchived)
    {
        var id = Guid.NewGuid();
        return new BrowserProfile
        {
            ProfileId = id,
            Name = name,
            StartupUrl = url,
            StorageRelativePath = id.ToString("N"),
            IsFavorite = isFavorite,
            IsArchived = isArchived,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }
}
