using Switchly.Domain.Import;

namespace Switchly.Application.Import;

/// <summary>Orchestrates read-only preview and execution of Chrome/Edge imports (REQ-008, REQ-009).</summary>
public interface IBrowserImportService
{
    IReadOnlyList<DetectedBrowserProfile> DiscoverProfiles(ImportSourceBrowser? browserFilter = null);

    Task<ImportPreviewResult> PreviewAsync(
        DetectedBrowserProfile sourceProfile,
        ImportCategories categories,
        Guid? targetProfileId = null,
        CancellationToken cancellationToken = default);

    Task<ImportExecutionResult> ExecuteAsync(
        DetectedBrowserProfile sourceProfile,
        ImportCategories categories,
        Guid targetProfileId,
        CancellationToken cancellationToken = default);
}
