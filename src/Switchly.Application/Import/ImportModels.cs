using Switchly.Domain.Import;

namespace Switchly.Application.Import;

public sealed record DetectedBrowserProfile(
    ImportSourceBrowser Browser,
    string ProfileDirectoryName,
    string DisplayLabel,
    string ProfileDirectoryPath,
    bool HasBookmarks,
    bool HasHistory,
    bool HasSessions)
{
    public string FriendlyLabel => $"{Browser} · {DisplayLabel} ({ProfileDirectoryName})";

    public override string ToString() => FriendlyLabel;
}

public sealed record ImportWarning(
    string Category,
    string Code,
    string Message);

public sealed record ImportedBookmarkNode(
    string Title,
    string? Url,
    IReadOnlyList<ImportedBookmarkNode> Children)
{
    public bool IsFolder => Url is null;
}

public sealed record BookmarkParseResult(
    IReadOnlyList<ImportedBookmarkNode> Roots,
    int TotalUrlCount,
    int SkippedCount,
    IReadOnlyList<ImportWarning> Warnings);

public sealed record ImportedHistoryItem(
    string Url,
    string? Title,
    int VisitCount,
    DateTimeOffset VisitedAtUtc);

public sealed record HistoryParseResult(
    IReadOnlyList<ImportedHistoryItem> Entries,
    int SkippedCount,
    bool IsDatabaseLocked,
    IReadOnlyList<ImportWarning> Warnings);

public sealed record ImportedTabItem(
    string Url,
    string? Title);

public sealed record OpenTabsParseResult(
    IReadOnlyList<ImportedTabItem> Tabs,
    int SkippedCount,
    IReadOnlyList<ImportWarning> Warnings);

public sealed record ImportPreviewResult(
    Guid ImportJobId,
    DetectedBrowserProfile SourceProfile,
    ImportCategories SelectedCategories,
    int BookmarkCount,
    int BookmarkDuplicateCount,
    int HistoryCount,
    int HistoryDuplicateCount,
    int OpenTabCount,
    bool IsHistoryDatabaseLocked,
    IReadOnlyList<ImportWarning> Warnings);

public sealed record ImportExecutionResult(
    Guid ImportJobId,
    ImportJobStatus Status,
    int BookmarksImported,
    int BookmarksSkipped,
    int HistoryImported,
    int HistorySkipped,
    int OpenTabsImported,
    int OpenTabsSkipped,
    bool IsHistoryDatabaseLocked,
    IReadOnlyList<ImportWarning> Warnings);
