using System.Text.Json;
using Switchly.Application.Browser;
using Switchly.Domain.History;
using Switchly.Domain.Import;

namespace Switchly.Application.Import;

/// <summary>
/// Orchestrates read-only preview and execution of local Chrome/Edge imports (REQ-008, REQ-009).
/// Cookies, login sessions, and passwords are never read or imported.
/// </summary>
public sealed class BrowserImportService : IBrowserImportService
{
    private readonly IBrowserProfileDiscovery _discovery;
    private readonly IBookmarkImporter _bookmarkImporter;
    private readonly IHistoryImporter _historyImporter;
    private readonly IOpenTabImporter _openTabImporter;
    private readonly IBookmarkService _bookmarkService;
    private readonly IHistoryService _historyService;
    private readonly ITabSessionService _tabSessionService;
    private readonly IImportJobRepository _importJobRepository;
    private readonly TimeProvider _timeProvider;

    public BrowserImportService(
        IBrowserProfileDiscovery discovery,
        IBookmarkImporter bookmarkImporter,
        IHistoryImporter historyImporter,
        IOpenTabImporter openTabImporter,
        IBookmarkService bookmarkService,
        IHistoryService historyService,
        ITabSessionService tabSessionService,
        IImportJobRepository importJobRepository,
        TimeProvider? timeProvider = null)
    {
        _discovery = discovery ?? throw new ArgumentNullException(nameof(discovery));
        _bookmarkImporter = bookmarkImporter ?? throw new ArgumentNullException(nameof(bookmarkImporter));
        _historyImporter = historyImporter ?? throw new ArgumentNullException(nameof(historyImporter));
        _openTabImporter = openTabImporter ?? throw new ArgumentNullException(nameof(openTabImporter));
        _bookmarkService = bookmarkService ?? throw new ArgumentNullException(nameof(bookmarkService));
        _historyService = historyService ?? throw new ArgumentNullException(nameof(historyService));
        _tabSessionService = tabSessionService ?? throw new ArgumentNullException(nameof(tabSessionService));
        _importJobRepository = importJobRepository ?? throw new ArgumentNullException(nameof(importJobRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public IReadOnlyList<DetectedBrowserProfile> DiscoverProfiles(ImportSourceBrowser? browserFilter = null)
        => _discovery.DiscoverLocalProfiles(browserFilter);

    public async Task<ImportPreviewResult> PreviewAsync(
        DetectedBrowserProfile sourceProfile,
        ImportCategories categories,
        Guid? targetProfileId = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceProfile);

        var warnings = new List<ImportWarning>();
        var bookmarkCount = 0;
        var bookmarkDuplicateCount = 0;
        var historyCount = 0;
        var historyDuplicateCount = 0;
        var openTabCount = 0;
        var isHistoryLocked = false;

        if (categories.HasFlag(ImportCategories.Bookmarks))
        {
            var bookmarksPath = Path.Combine(sourceProfile.ProfileDirectoryPath, "Bookmarks");
            var parsed = _bookmarkImporter.ParseChromiumBookmarksFile(bookmarksPath);
            bookmarkCount = parsed.TotalUrlCount;
            AddWarningsCapped(warnings, parsed.Warnings);

            if (targetProfileId.HasValue && bookmarkCount > 0)
            {
                var existing = await _bookmarkService.ListAsync(targetProfileId.Value, cancellationToken: cancellationToken);
                var existingUrls = new HashSet<string>(
                    existing.Where(b => b.Url is not null).Select(b => b.Url!),
                    StringComparer.OrdinalIgnoreCase);
                bookmarkDuplicateCount = CountDuplicateBookmarkUrls(parsed.Roots, existingUrls);
            }
        }

        if (categories.HasFlag(ImportCategories.History))
        {
            var historyPath = Path.Combine(sourceProfile.ProfileDirectoryPath, "History");
            var parsed = _historyImporter.ParseChromiumHistoryFile(historyPath);
            historyCount = parsed.Entries.Count;
            isHistoryLocked = parsed.IsDatabaseLocked;
            AddWarningsCapped(warnings, parsed.Warnings);

            if (targetProfileId.HasValue && historyCount > 0)
            {
                var existing = await _historyService.ListAsync(targetProfileId.Value, limit: 1000, cancellationToken: cancellationToken);
                var existingUrls = new HashSet<string>(existing.Select(h => h.Url), StringComparer.OrdinalIgnoreCase);
                historyDuplicateCount = parsed.Entries.Count(e => existingUrls.Contains(e.Url));
            }
        }

        if (categories.HasFlag(ImportCategories.OpenTabs))
        {
            var parsed = _openTabImporter.ParseChromiumSessionsDirectory(sourceProfile.ProfileDirectoryPath);
            openTabCount = parsed.Tabs.Count;
            AddWarningsCapped(warnings, parsed.Warnings);
        }

        var job = new ImportJob
        {
            ImportJobId = Guid.NewGuid(),
            SourceBrowser = sourceProfile.Browser,
            SourceProfileLabel = SanitizeProfileLabel(sourceProfile.DisplayLabel),
            SelectedCategoriesJson = SerializeCategories(categories),
            TargetProfileId = targetProfileId,
            Status = ImportJobStatus.Preview,
            ResultSummaryJson = SerializeSummary(new
            {
                mode = "preview",
                bookmarkCount,
                bookmarkDuplicateCount,
                historyCount,
                historyDuplicateCount,
                openTabCount,
                isHistoryLocked,
                warningCount = warnings.Count,
                warningCodes = warnings.Select(w => w.Code).Distinct().ToArray(),
            }),
            CreatedAtUtc = _timeProvider.GetUtcNow(),
            CompletedAtUtc = null,
        };

        await _importJobRepository.AddAsync(job, cancellationToken);

        return new ImportPreviewResult(
            job.ImportJobId,
            sourceProfile,
            categories,
            bookmarkCount,
            bookmarkDuplicateCount,
            historyCount,
            historyDuplicateCount,
            openTabCount,
            isHistoryLocked,
            warnings);
    }

    public async Task<ImportExecutionResult> ExecuteAsync(
        DetectedBrowserProfile sourceProfile,
        ImportCategories categories,
        Guid targetProfileId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceProfile);
        if (targetProfileId == Guid.Empty)
        {
            throw new ArgumentException("Target profile ID is required.", nameof(targetProfileId));
        }

        var job = new ImportJob
        {
            ImportJobId = Guid.NewGuid(),
            SourceBrowser = sourceProfile.Browser,
            SourceProfileLabel = SanitizeProfileLabel(sourceProfile.DisplayLabel),
            SelectedCategoriesJson = SerializeCategories(categories),
            TargetProfileId = targetProfileId,
            Status = ImportJobStatus.Running,
            ResultSummaryJson = "{}",
            CreatedAtUtc = _timeProvider.GetUtcNow(),
            CompletedAtUtc = null,
        };
        await _importJobRepository.AddAsync(job, cancellationToken);

        var warnings = new List<ImportWarning>();
        var bookmarksImported = 0;
        var bookmarksSkipped = 0;
        var historyImported = 0;
        var historySkipped = 0;
        var openTabsImported = 0;
        var openTabsSkipped = 0;
        var isHistoryLocked = false;
        var hadCategoryFailure = false;

        try
        {
            if (categories.HasFlag(ImportCategories.Bookmarks))
            {
                var bookmarksPath = Path.Combine(sourceProfile.ProfileDirectoryPath, "Bookmarks");
                var parsed = _bookmarkImporter.ParseChromiumBookmarksFile(bookmarksPath);
                bookmarksSkipped += parsed.SkippedCount;
                AddWarningsCapped(warnings, parsed.Warnings);
                if (parsed.Warnings.Any(w => w.Code.EndsWith("Error", StringComparison.OrdinalIgnoreCase) || w.Code.EndsWith("NotFound", StringComparison.OrdinalIgnoreCase)))
                {
                    hadCategoryFailure = true;
                }

                var existing = await _bookmarkService.ListAsync(targetProfileId, cancellationToken: cancellationToken);
                var existingUrls = new HashSet<string>(
                    existing.Where(b => b.Url is not null).Select(b => b.Url!),
                    StringComparer.OrdinalIgnoreCase);

                foreach (var root in parsed.Roots)
                {
                    var (imported, skipped) = await ImportBookmarkNodeAsync(
                        targetProfileId,
                        root,
                        parentBookmarkId: null,
                        existingUrls,
                        cancellationToken);
                    bookmarksImported += imported;
                    bookmarksSkipped += skipped;
                }
            }

            if (categories.HasFlag(ImportCategories.History))
            {
                var historyPath = Path.Combine(sourceProfile.ProfileDirectoryPath, "History");
                var parsed = _historyImporter.ParseChromiumHistoryFile(historyPath);
                historySkipped += parsed.SkippedCount;
                isHistoryLocked = parsed.IsDatabaseLocked;
                AddWarningsCapped(warnings, parsed.Warnings);
                if (isHistoryLocked || parsed.Warnings.Any(w => w.Code.Contains("Error", StringComparison.OrdinalIgnoreCase) || w.Code.Contains("Malformed", StringComparison.OrdinalIgnoreCase) || w.Code.Contains("Locked", StringComparison.OrdinalIgnoreCase)))
                {
                    hadCategoryFailure = true;
                }

                var existing = await _historyService.ListAsync(targetProfileId, limit: 1000, cancellationToken: cancellationToken);
                var existingUrls = new HashSet<string>(existing.Select(h => h.Url), StringComparer.OrdinalIgnoreCase);

                foreach (var entry in parsed.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!existingUrls.Add(entry.Url))
                    {
                        historySkipped++;
                        continue;
                    }

                    try
                    {
                        await _historyService.RecordVisitAsync(
                            targetProfileId,
                            entry.Url,
                            entry.Title,
                            HistoryTransitionKind.Link,
                            retainQueryString: false,
                            cancellationToken);
                        historyImported++;
                    }
                    catch (ArgumentException)
                    {
                        historySkipped++;
                    }
                }
            }

            if (categories.HasFlag(ImportCategories.OpenTabs))
            {
                var parsed = _openTabImporter.ParseChromiumSessionsDirectory(sourceProfile.ProfileDirectoryPath);
                openTabsSkipped += parsed.SkippedCount;
                AddWarningsCapped(warnings, parsed.Warnings);
                if (parsed.Warnings.Any(w => w.Code.Contains("Unsupported", StringComparison.OrdinalIgnoreCase) || w.Code.Contains("Error", StringComparison.OrdinalIgnoreCase)))
                {
                    hadCategoryFailure = true;
                }

                if (parsed.Tabs.Count > 0)
                {
                    var existingTabs = await _tabSessionService.GetRestorableTabsAsync(targetProfileId, cancellationToken);
                    var existingUrls = new HashSet<string>(existingTabs.Select(t => t.Url), StringComparer.OrdinalIgnoreCase);
                    var merged = existingTabs
                        .Select(t => new TabSnapshotInput(t.Url, t.Title, t.IsPinned))
                        .ToList();

                    foreach (var tab in parsed.Tabs)
                    {
                        if (!existingUrls.Add(tab.Url))
                        {
                            openTabsSkipped++;
                            continue;
                        }

                        merged.Add(new TabSnapshotInput(tab.Url, tab.Title, false));
                        openTabsImported++;
                    }

                    if (openTabsImported > 0)
                    {
                        var windowId = existingTabs.FirstOrDefault()?.WindowId ?? Guid.NewGuid();
                        await _tabSessionService.SaveOpenTabsAsync(targetProfileId, windowId, merged, cancellationToken);
                    }
                }
            }

            var totalImported = bookmarksImported + historyImported + openTabsImported;
            job.Status = (hadCategoryFailure || warnings.Count > 0)
                ? (totalImported > 0 ? ImportJobStatus.Partial : ImportJobStatus.Failed)
                : ImportJobStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            job.Status = ImportJobStatus.Cancelled;
            throw;
        }
        finally
        {
            job.CompletedAtUtc = _timeProvider.GetUtcNow();
            job.ResultSummaryJson = SerializeSummary(new
            {
                mode = "execute",
                status = job.Status.ToString(),
                bookmarksImported,
                bookmarksSkipped,
                historyImported,
                historySkipped,
                openTabsImported,
                openTabsSkipped,
                isHistoryLocked,
                warningCount = warnings.Count,
                warningCodes = warnings.Select(w => w.Code).Distinct().ToArray(),
            });
            await _importJobRepository.UpdateAsync(job, CancellationToken.None);
        }

        return new ImportExecutionResult(
            job.ImportJobId,
            job.Status,
            bookmarksImported,
            bookmarksSkipped,
            historyImported,
            historySkipped,
            openTabsImported,
            openTabsSkipped,
            isHistoryLocked,
            warnings);
    }

    private async Task<(int Imported, int Skipped)> ImportBookmarkNodeAsync(
        Guid targetProfileId,
        ImportedBookmarkNode node,
        Guid? parentBookmarkId,
        HashSet<string> existingUrls,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (node.IsFolder)
        {
            if (node.Children.Count == 0)
            {
                return (0, 0);
            }

            var folder = await _bookmarkService.AddFolderAsync(targetProfileId, node.Title, parentBookmarkId, cancellationToken);
            var imported = 0;
            var skipped = 0;

            foreach (var child in node.Children)
            {
                var (childImported, childSkipped) = await ImportBookmarkNodeAsync(
                    targetProfileId,
                    child,
                    folder.BookmarkId,
                    existingUrls,
                    cancellationToken);
                imported += childImported;
                skipped += childSkipped;
            }

            return (imported, skipped);
        }

        if (node.Url is null || !existingUrls.Add(node.Url))
        {
            return (0, 1);
        }

        try
        {
            await _bookmarkService.AddBookmarkAsync(targetProfileId, node.Title, node.Url, parentBookmarkId, cancellationToken);
            return (1, 0);
        }
        catch (ArgumentException)
        {
            return (0, 1);
        }
    }

    private static int CountDuplicateBookmarkUrls(IEnumerable<ImportedBookmarkNode> nodes, HashSet<string> existingUrls)
    {
        var count = 0;
        foreach (var node in nodes)
        {
            if (node.Url is not null && existingUrls.Contains(node.Url))
            {
                count++;
            }

            if (node.Children.Count > 0)
            {
                count += CountDuplicateBookmarkUrls(node.Children, existingUrls);
            }
        }

        return count;
    }

    private static void AddWarningsCapped(List<ImportWarning> target, IReadOnlyList<ImportWarning> source)
    {
        foreach (var w in source)
        {
            if (target.Count >= ImportLimits.MaxWarningsPerJob)
            {
                break;
            }

            target.Add(w);
        }
    }

    private static string SanitizeProfileLabel(string label)
    {
        var trimmed = (label ?? string.Empty).Trim();
        if (trimmed.Length == 0)
        {
            return "Default";
        }

        return trimmed.Length > 200 ? trimmed[..200] : trimmed;
    }

    private static string SerializeCategories(ImportCategories categories)
    {
        var list = new List<string>();
        if (categories.HasFlag(ImportCategories.Bookmarks))
        {
            list.Add("Bookmarks");
        }

        if (categories.HasFlag(ImportCategories.History))
        {
            list.Add("History");
        }

        if (categories.HasFlag(ImportCategories.OpenTabs))
        {
            list.Add("OpenTabs");
        }

        return JsonSerializer.Serialize(list);
    }

    private static string SerializeSummary(object summary) => JsonSerializer.Serialize(summary);
}
