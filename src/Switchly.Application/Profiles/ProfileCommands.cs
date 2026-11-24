namespace Switchly.Application.Profiles;

public sealed record CreateProfileCommand(
    string Name,
    string? StartupUrl = null,
    string? ColorHex = null,
    string? IconKey = null,
    bool IsFavorite = false,
    bool RestoreLastSession = true,
    bool PersistSessionCookies = true);

public sealed record UpdateProfileCommand(
    Guid ProfileId,
    string Name,
    string? StartupUrl,
    string? ColorHex,
    string? IconKey,
    bool IsFavorite,
    bool RestoreLastSession,
    bool PersistSessionCookies);

public sealed record DuplicateProfileCommand(
    Guid SourceProfileId,
    string? NewName = null);

public sealed class ProfileNotFoundException : InvalidOperationException
{
    public ProfileNotFoundException(Guid profileId)
        : base($"Profile '{profileId}' was not found.")
    {
        ProfileId = profileId;
    }

    public Guid ProfileId { get; }
}

public sealed class ProfileInUseException : InvalidOperationException
{
    public ProfileInUseException(Guid profileId)
        : base($"Profile '{profileId}' is currently active and must be closed before deletion.")
    {
        ProfileId = profileId;
    }

    public Guid ProfileId { get; }
}
