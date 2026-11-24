namespace Switchly.Domain.Import;

/// <summary>
/// Audit record for a local browser import preview or execution (ENT-012).
/// Never stores passwords, cookies, or raw sensitive query parameters.
/// </summary>
public sealed class ImportJob
{
    public Guid ImportJobId { get; set; }

    public ImportSourceBrowser SourceBrowser { get; set; }

    public string SourceProfileLabel { get; set; } = string.Empty;

    public string SelectedCategoriesJson { get; set; } = "[]";

    public Guid? TargetProfileId { get; set; }

    public ImportJobStatus Status { get; set; } = ImportJobStatus.Preview;

    public string ResultSummaryJson { get; set; } = "{}";

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
