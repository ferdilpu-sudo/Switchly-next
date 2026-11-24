namespace Switchly.Domain.History;

public enum HistoryTransitionKind
{
    Link,
    Typed,
    Reload,
    Restore,
    Other,
}

/// <summary>Profile-scoped browsing history projection (ENT-006, REQ-006).</summary>
public sealed class HistoryEntry
{
    public Guid HistoryId { get; set; }

    public Guid ProfileId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string? Title { get; set; }

    public DateTimeOffset VisitedAtUtc { get; set; }

    public int VisitCount { get; set; } = 1;

    public HistoryTransitionKind TransitionKind { get; set; } = HistoryTransitionKind.Link;
}
