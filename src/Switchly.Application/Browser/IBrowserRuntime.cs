namespace Switchly.Application.Browser;

/// <summary>Engine-level lifecycle for the embedded browser runtime.</summary>
public interface IBrowserRuntime
{
    /// <summary>Initialize the runtime once per process with the given root cache directory (ADR-011).</summary>
    bool Initialize(string rootCachePath, string? logDirectory = null);

    /// <summary>Shut the runtime down. Must be called on the same thread as Initialize.</summary>
    void Shutdown();

    /// <summary>Create an isolated profile context backed by a unique direct-child storage path.</summary>
    IBrowserProfileContext CreateProfileContext(Guid profileId, string storagePath, bool persistSessionCookies = true);

    /// <summary>Create a browser window/tab host bound to an isolated profile context.</summary>
    IBrowserWindowHost CreateWindowHost(IBrowserProfileContext profileContext, string initialUrl);
}
