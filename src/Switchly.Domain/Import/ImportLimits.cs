namespace Switchly.Domain.Import;

/// <summary>
/// Hard bounds protecting against memory/disk exhaustion when parsing untrusted browser data (schema.md §5).
/// </summary>
public static class ImportLimits
{
    public const int MaxBookmarks = 20_000;
    public const int MaxBookmarkDepth = 32;
    public const int MaxHistoryEntries = 20_000;
    public const int MaxOpenTabs = 500;
    public const long MaxSourceFileBytes = 50L * 1024 * 1024;
    public const int MaxWarningsPerJob = 100;
}
