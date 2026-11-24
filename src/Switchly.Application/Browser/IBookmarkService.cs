using Switchly.Domain.Bookmarks;

namespace Switchly.Application.Browser;

/// <summary>Profile-scoped bookmark management use cases (REQ-006, AC-007, REQ-016).</summary>
public interface IBookmarkService
{
    Task<Bookmark> AddBookmarkAsync(Guid profileId, string title, string url, Guid? parentBookmarkId = null, CancellationToken cancellationToken = default);

    Task<Bookmark> AddFolderAsync(Guid profileId, string title, Guid? parentBookmarkId = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid profileId, Guid bookmarkId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Bookmark>> ListAsync(Guid profileId, string? searchText = null, CancellationToken cancellationToken = default);
}
