using Switchly.Domain.Bookmarks;

namespace Switchly.Application.Browser;

/// <summary>Persistence port for profile-scoped Bookmarks (ENT-005).</summary>
public interface IBookmarkRepository
{
    Task<Bookmark?> GetByIdAsync(Guid bookmarkId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Bookmark>> ListByProfileAsync(Guid profileId, string? searchText = null, CancellationToken cancellationToken = default);

    Task<int> GetNextPositionAsync(Guid profileId, Guid? parentBookmarkId, CancellationToken cancellationToken = default);

    Task AddAsync(Bookmark bookmark, CancellationToken cancellationToken = default);

    Task DeleteAsync(Bookmark bookmark, CancellationToken cancellationToken = default);
}
