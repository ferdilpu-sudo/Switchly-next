namespace Switchly.Application.Import;

/// <summary>Read-only parser for Chromium Bookmarks JSON and Netscape HTML exports (TASK-024).</summary>
public interface IBookmarkImporter
{
    BookmarkParseResult ParseChromiumBookmarksFile(string bookmarksFilePath);

    BookmarkParseResult ParseNetscapeHtmlFile(string htmlFilePath);
}
