namespace Switchly.Application.Browser;

/// <summary>
/// An isolated per-profile browser storage context. Two profiles must never
/// share one context (REQ-003).
/// </summary>
public interface IBrowserProfileContext : IDisposable
{
    Guid ProfileId { get; }

    /// <summary>The unique on-disk storage path for this profile.</summary>
    string StoragePath { get; }
}
