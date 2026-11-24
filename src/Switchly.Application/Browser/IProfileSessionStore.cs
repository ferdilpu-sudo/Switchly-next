using Switchly.Domain.Profiles;

namespace Switchly.Application.Browser;

/// <summary>Short-lived persistence port used by the singleton ProfileSessionManager to avoid captive DbContext.</summary>
public interface IProfileSessionStore
{
    Task<BrowserProfile?> GetProfileAsync(Guid profileId, CancellationToken cancellationToken = default);

    Task RecordLastOpenedAsync(Guid profileId, DateTimeOffset openedAtUtc, CancellationToken cancellationToken = default);
}
