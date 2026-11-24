namespace Switchly.Domain.Profiles;

/// <summary>Runtime lifecycle states of a browser profile (architecture.md §5.2).</summary>
public enum ProfileRuntimeState
{
    Closed,
    Starting,
    Active,
    Suspending,
    Suspended,
    Error,
}
