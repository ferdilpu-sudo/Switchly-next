using Switchly.Domain.Profiles;

namespace Switchly.Application.Profiles;

public sealed record ProfileFilterCriteria(
    string? SearchText = null,
    bool FavoritesOnly = false,
    bool IncludeArchived = false,
    bool ArchivedOnly = false,
    IReadOnlySet<Guid>? AllowedProfileIds = null);

/// <summary>Pure search and filter logic for profiles without altering underlying membership or order (REQ-016, AC-016, TEST-024).</summary>
public static class ProfileFilter
{
    public static IReadOnlyList<BrowserProfile> Apply(
        IEnumerable<BrowserProfile> profiles,
        ProfileFilterCriteria criteria)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(criteria);

        var query = profiles;

        if (criteria.ArchivedOnly)
        {
            query = query.Where(p => p.IsArchived);
        }
        else if (!criteria.IncludeArchived)
        {
            query = query.Where(p => !p.IsArchived);
        }

        if (criteria.FavoritesOnly)
        {
            query = query.Where(p => p.IsFavorite);
        }

        if (criteria.AllowedProfileIds is not null)
        {
            query = query.Where(p => criteria.AllowedProfileIds.Contains(p.ProfileId));
        }

        if (!string.IsNullOrWhiteSpace(criteria.SearchText))
        {
            var term = criteria.SearchText.Trim();
            query = query.Where(p =>
                p.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(p.StartupUrl) && p.StartupUrl.Contains(term, StringComparison.OrdinalIgnoreCase)));
        }

        return query.ToList();
    }
}
