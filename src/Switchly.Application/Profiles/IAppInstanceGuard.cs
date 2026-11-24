namespace Switchly.Application.Profiles;

/// <summary>
/// App-level single-instance guard required by CEF Chrome Runtime's ProcessSingleton
/// on RootCachePath (ADR-011, TEST-017).
/// </summary>
public interface IAppInstanceGuard : IDisposable
{
    bool TryAcquire();
}
