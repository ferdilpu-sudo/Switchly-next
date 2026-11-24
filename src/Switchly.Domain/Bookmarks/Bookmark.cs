namespace Switchly.Domain.Bookmarks;

/// <summary>Application-managed per-profile bookmark or folder (ENT-005, REQ-006).</summary>
public sealed class Bookmark
{
    public Guid BookmarkId { get; set; }

    public Guid ProfileId { get; set; }

    public Guid? ParentBookmarkId { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Null for folders; absolute http/https URL for bookmark links.</summary>
    public string? Url { get; set; }

    public int Position { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
