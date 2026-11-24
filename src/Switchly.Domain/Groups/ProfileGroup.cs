namespace Switchly.Domain.Groups;

public enum GroupKind
{
    Manual,
    WebsiteDomain,
}

/// <summary>A manual or website-domain profile group (ENT-002).</summary>
public sealed class ProfileGroup
{
    public Guid GroupId { get; set; }

    public string Name { get; set; } = string.Empty;

    public GroupKind Kind { get; set; }

    public string? DomainKey { get; set; }

    public bool IsHidden { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
