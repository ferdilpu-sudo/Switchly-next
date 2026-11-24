using Switchly.Domain.Bookmarks;
using Switchly.Domain.History;

namespace Switchly.Application.Browser;

public sealed class BookmarkService : IBookmarkService
{
    private readonly IBookmarkRepository _repository;
    private readonly TimeProvider _timeProvider;

    public BookmarkService(IBookmarkRepository repository, TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<Bookmark> AddBookmarkAsync(
        Guid profileId,
        string title,
        string url,
        Guid? parentBookmarkId = null,
        CancellationToken cancellationToken = default)
    {
        var sanitizedUrl = UrlSanitizer.SanitizeHttpUrl(url, retainQueryString: true);
        var normalizedTitle = UrlSanitizer.NormalizeTitle(title, sanitizedUrl);
        var position = await _repository.GetNextPositionAsync(profileId, parentBookmarkId, cancellationToken);
        var now = _timeProvider.GetUtcNow();

        var bookmark = new Bookmark
        {
            BookmarkId = Guid.NewGuid(),
            ProfileId = profileId,
            ParentBookmarkId = parentBookmarkId,
            Title = normalizedTitle,
            Url = sanitizedUrl,
            Position = position,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await _repository.AddAsync(bookmark, cancellationToken);
        return bookmark;
    }

    public async Task<Bookmark> AddFolderAsync(
        Guid profileId,
        string title,
        Guid? parentBookmarkId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Folder title is required.", nameof(title));
        }

        var position = await _repository.GetNextPositionAsync(profileId, parentBookmarkId, cancellationToken);
        var now = _timeProvider.GetUtcNow();

        var folder = new Bookmark
        {
            BookmarkId = Guid.NewGuid(),
            ProfileId = profileId,
            ParentBookmarkId = parentBookmarkId,
            Title = UrlSanitizer.NormalizeTitle(title, "Folder"),
            Url = null,
            Position = position,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        await _repository.AddAsync(folder, cancellationToken);
        return folder;
    }

    public async Task DeleteAsync(Guid profileId, Guid bookmarkId, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(bookmarkId, cancellationToken);
        if (existing is null || existing.ProfileId != profileId)
        {
            return;
        }

        await _repository.DeleteAsync(existing, cancellationToken);
    }

    public Task<IReadOnlyList<Bookmark>> ListAsync(Guid profileId, string? searchText = null, CancellationToken cancellationToken = default)
        => _repository.ListByProfileAsync(profileId, searchText, cancellationToken);
}
