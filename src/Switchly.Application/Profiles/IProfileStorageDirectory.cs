namespace Switchly.Application.Profiles;

/// <summary>Filesystem boundary for per-profile isolated browser storage directories (ADR-011).</summary>
public interface IProfileStorageDirectory
{
    string EnsureCreated(string storageRelativePath);

    void DeleteIfExists(string storageRelativePath);

    /// <summary>Deletes any profile directories queued for deferred removal before CEF starts.</summary>
    void ProcessPendingDeletes();
}
