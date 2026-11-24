using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Switchly.Application.Import;
using Switchly.Domain.History;
using Switchly.Domain.Import;

namespace Switchly.Infrastructure.Import;

/// <summary>
/// Read-only parser for Chromium Bookmarks JSON and Netscape Bookmark HTML exports (TASK-024).
/// Rejects unsafe schemes (javascript:, file:, data:) via UrlSanitizer.
/// </summary>
public sealed partial class ChromiumBookmarkImporter : IBookmarkImporter
{
    private static readonly string[] RootKeys = ["bookmark_bar", "other", "synced"];

    public BookmarkParseResult ParseChromiumBookmarksFile(string bookmarksFilePath)
    {
        var warnings = new List<ImportWarning>();
        if (string.IsNullOrWhiteSpace(bookmarksFilePath) || !File.Exists(bookmarksFilePath))
        {
            warnings.Add(new ImportWarning("Bookmarks", "BookmarksNotFound", "File Bookmarks tidak ditemukan di profil sumber."));
            return new BookmarkParseResult(Array.Empty<ImportedBookmarkNode>(), 0, 0, warnings);
        }

        try
        {
            var info = new FileInfo(bookmarksFilePath);
            if (info.Length > ImportLimits.MaxSourceFileBytes)
            {
                warnings.Add(new ImportWarning("Bookmarks", "BookmarksFileTooLarge", "Ukuran file Bookmarks melebihi batas aman impor."));
                return new BookmarkParseResult(Array.Empty<ImportedBookmarkNode>(), 0, 0, warnings);
            }

            using var stream = new FileStream(bookmarksFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions { MaxDepth = 64 });

            if (!doc.RootElement.TryGetProperty("roots", out var rootsObj) || rootsObj.ValueKind != JsonValueKind.Object)
            {
                warnings.Add(new ImportWarning("Bookmarks", "BookmarksMalformedError", "Format file Bookmarks tidak valid (bagian roots tidak ditemukan)."));
                return new BookmarkParseResult(Array.Empty<ImportedBookmarkNode>(), 0, 0, warnings);
            }

            var roots = new List<ImportedBookmarkNode>();
            var state = new ParseState(warnings);

            foreach (var rootKey in RootKeys)
            {
                if (!rootsObj.TryGetProperty(rootKey, out var rootElement) || rootElement.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var node = ParseJsonNode(rootElement, depth: 0, state);
                if (node is not null && (node.Url is not null || node.Children.Count > 0))
                {
                    roots.Add(node);
                }
            }

            if (state.UnsafeUrlCount > 0)
            {
                warnings.Add(new ImportWarning(
                    "Bookmarks",
                    "BookmarksUnsafeUrlsSkipped",
                    $"{state.UnsafeUrlCount} bookmark dengan skema URL tidak didukung/tidak aman dilewati."));
            }

            return new BookmarkParseResult(roots, state.UrlCount, state.SkippedCount, warnings);
        }
        catch (JsonException)
        {
            warnings.Add(new ImportWarning("Bookmarks", "BookmarksMalformedError", "File Bookmarks rusak atau bukan JSON yang valid."));
            return new BookmarkParseResult(Array.Empty<ImportedBookmarkNode>(), 0, 0, warnings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add(new ImportWarning("Bookmarks", "BookmarksReadError", "Tidak dapat membaca file Bookmarks dari profil sumber."));
            return new BookmarkParseResult(Array.Empty<ImportedBookmarkNode>(), 0, 0, warnings);
        }
    }

    public BookmarkParseResult ParseNetscapeHtmlFile(string htmlFilePath)
    {
        var warnings = new List<ImportWarning>();
        if (string.IsNullOrWhiteSpace(htmlFilePath) || !File.Exists(htmlFilePath))
        {
            warnings.Add(new ImportWarning("Bookmarks", "BookmarksNotFound", "File HTML bookmark tidak ditemukan."));
            return new BookmarkParseResult(Array.Empty<ImportedBookmarkNode>(), 0, 0, warnings);
        }

        try
        {
            var info = new FileInfo(htmlFilePath);
            if (info.Length > ImportLimits.MaxSourceFileBytes)
            {
                warnings.Add(new ImportWarning("Bookmarks", "BookmarksFileTooLarge", "Ukuran file HTML bookmark melebihi batas aman impor."));
                return new BookmarkParseResult(Array.Empty<ImportedBookmarkNode>(), 0, 0, warnings);
            }

            string content;
            using (var stream = new FileStream(htmlFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                content = reader.ReadToEnd();
            }

            var items = new List<ImportedBookmarkNode>();
            var skipped = 0;
            var unsafeCount = 0;

            foreach (Match match in NetscapeAnchorRegex().Matches(content))
            {
                if (items.Count >= ImportLimits.MaxBookmarks)
                {
                    warnings.Add(new ImportWarning("Bookmarks", "BookmarksLimitReached", $"Batas maksimum {ImportLimits.MaxBookmarks} bookmark tercapai."));
                    break;
                }

                var rawHref = WebUtility.HtmlDecode(match.Groups["href"].Value).Trim();
                var rawTitle = WebUtility.HtmlDecode(match.Groups["title"].Value).Trim();

                try
                {
                    var sanitized = UrlSanitizer.SanitizeHttpUrl(rawHref, retainQueryString: true);
                    var title = UrlSanitizer.NormalizeTitle(rawTitle, sanitized);
                    items.Add(new ImportedBookmarkNode(title, sanitized, Array.Empty<ImportedBookmarkNode>()));
                }
                catch (ArgumentException)
                {
                    skipped++;
                    unsafeCount++;
                }
            }

            if (unsafeCount > 0)
            {
                warnings.Add(new ImportWarning(
                    "Bookmarks",
                    "BookmarksUnsafeUrlsSkipped",
                    $"{unsafeCount} bookmark HTML dengan skema URL tidak aman dilewati."));
            }

            return new BookmarkParseResult(items, items.Count, skipped, warnings);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            warnings.Add(new ImportWarning("Bookmarks", "BookmarksReadError", "Tidak dapat membaca file HTML bookmark."));
            return new BookmarkParseResult(Array.Empty<ImportedBookmarkNode>(), 0, 0, warnings);
        }
    }

    private static ImportedBookmarkNode? ParseJsonNode(JsonElement element, int depth, ParseState state)
    {
        if (depth > ImportLimits.MaxBookmarkDepth)
        {
            state.SkippedCount++;
            return null;
        }

        if (state.UrlCount >= ImportLimits.MaxBookmarks)
        {
            if (!state.LimitWarningAdded)
            {
                state.LimitWarningAdded = true;
                state.Warnings.Add(new ImportWarning(
                    "Bookmarks",
                    "BookmarksLimitReached",
                    $"Batas maksimum {ImportLimits.MaxBookmarks} bookmark tercapai."));
            }

            return null;
        }

        var type = element.TryGetProperty("type", out var typeProp) && typeProp.ValueKind == JsonValueKind.String
            ? typeProp.GetString()
            : null;
        var name = element.TryGetProperty("name", out var nameProp) && nameProp.ValueKind == JsonValueKind.String
            ? nameProp.GetString() ?? string.Empty
            : string.Empty;

        if (string.Equals(type, "url", StringComparison.OrdinalIgnoreCase))
        {
            var rawUrl = element.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String
                ? urlProp.GetString()
                : null;

            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                state.SkippedCount++;
                return null;
            }

            try
            {
                var sanitized = UrlSanitizer.SanitizeHttpUrl(rawUrl, retainQueryString: true);
                var title = UrlSanitizer.NormalizeTitle(name, sanitized);
                state.UrlCount++;
                return new ImportedBookmarkNode(title, sanitized, Array.Empty<ImportedBookmarkNode>());
            }
            catch (ArgumentException)
            {
                state.SkippedCount++;
                state.UnsafeUrlCount++;
                return null;
            }
        }

        if (string.Equals(type, "folder", StringComparison.OrdinalIgnoreCase) ||
            element.TryGetProperty("children", out _))
        {
            var children = new List<ImportedBookmarkNode>();
            if (element.TryGetProperty("children", out var childrenProp) && childrenProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var childElement in childrenProp.EnumerateArray())
                {
                    if (childElement.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var child = ParseJsonNode(childElement, depth + 1, state);
                    if (child is not null)
                    {
                        children.Add(child);
                    }
                }
            }

            var folderTitle = string.IsNullOrWhiteSpace(name) ? "Folder" : UrlSanitizer.NormalizeTitle(name, "Folder");
            return new ImportedBookmarkNode(folderTitle, null, children);
        }

        return null;
    }

    [GeneratedRegex("<a\\s+[^>]*href\\s*=\\s*\"(?<href>[^\"]+)\"[^>]*>(?<title>.*?)</a>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex NetscapeAnchorRegex();

    private sealed class ParseState
    {
        public ParseState(List<ImportWarning> warnings)
        {
            Warnings = warnings;
        }

        public List<ImportWarning> Warnings { get; }

        public int UrlCount { get; set; }

        public int SkippedCount { get; set; }

        public int UnsafeUrlCount { get; set; }

        public bool LimitWarningAdded { get; set; }
    }
}
