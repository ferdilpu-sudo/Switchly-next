using Switchly.Domain.Profiles;

namespace Switchly.Application.Browser;

public sealed record ProfileSessionSnapshot(
    Guid ProfileId,
    ProfileRuntimeState State,
    int OpenWindowCount,
    string? LastError = null);

public sealed class ProfileArchivedException : InvalidOperationException
{
    public ProfileArchivedException(Guid profileId)
        : base($"Archived profile '{profileId}' must be unarchived before opening.")
    {
        ProfileId = profileId;
    }

    public Guid ProfileId { get; }
}
