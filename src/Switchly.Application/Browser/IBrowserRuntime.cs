namespace Switchly.Application.Browser;

/// <summary>Engine-level lifecycle for the embedded browser runtime.</summary>
public interface IBrowserRuntime
{
    /// <summary>Initialize the runtime once per process.</summary>
    void Initialize(string rootCachePath);

    /// <summary>Shut the runtime down. Must be called on the same thread as Initialize.</summary>
    void Shutdown();

    /// <summary>Create an isolated profile context backed by a unique storage path.</summary>
    IBrowserProfileContext CreateProfileContext(Guid profileId, string storagePath);
}
