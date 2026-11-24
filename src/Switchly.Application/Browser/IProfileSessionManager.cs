using Switchly.Domain.Profiles;

namespace Switchly.Application.Browser;

/// <summary>Coordinates isolated per-profile browser contexts and window lifecycles (REQ-003, REQ-005).</summary>
public interface IProfileSessionManager : IDisposable
{
    event Action<ProfileSessionSnapshot>? StateChanged;

    Task<IBrowserProfileContext> OpenProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task<IBrowserWindowHost> OpenWindowAsync(Guid profileId, string? initialUrl = null, CancellationToken cancellationToken = default);

    void CloseWindow(Guid profileId, IBrowserWindowHost windowHost);

    Task CloseProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    ProfileRuntimeState GetState(Guid profileId);

    IReadOnlyList<ProfileSessionSnapshot> GetActiveSessions();
}
