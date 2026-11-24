namespace Switchly.Domain.Tabs;

public enum TabRestoreState
{
    Open,
    Closed,
    Discarded,
}

/// <summary>Restorable tab metadata scoped to one BrowserProfile (ENT-004, REQ-005, REQ-006).</summary>
public sealed class BrowserTabSession
{
    public Guid TabId { get; set; }

    public Guid ProfileId { get; set; }

    public Guid WindowId { get; set; }

    public string Url { get; set; } = string.Empty;

    public string? Title { get; set; }

    public int Position { get; set; }

    public bool IsPinned { get; set; }

    public DateTimeOffset LastActiveAtUtc { get; set; }

    public TabRestoreState RestoreState { get; set; } = TabRestoreState.Open;
}
