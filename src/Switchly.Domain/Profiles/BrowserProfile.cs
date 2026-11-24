namespace Switchly.Domain.Profiles;

/// <summary>A browser profile (ENT-001): identity and lifecycle metadata only.</summary>
public sealed class BrowserProfile
{
    public Guid ProfileId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? ColorHex { get; set; }

    public string? IconKey { get; set; }

    public string? StartupUrl { get; set; }

    public string StorageRelativePath { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? LastOpenedAtUtc { get; set; }

    public bool IsFavorite { get; set; }

    public bool IsArchived { get; set; }

    public bool RestoreLastSession { get; set; } = true;

    public bool PersistSessionCookies { get; set; } = true;
}
