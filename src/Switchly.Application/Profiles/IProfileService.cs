using Switchly.Domain.Profiles;

namespace Switchly.Application.Profiles;

/// <summary>Application use cases for profile lifecycle management (REQ-002, REQ-003, REQ-015).</summary>
public interface IProfileService
{
    Task<BrowserProfile> CreateAsync(CreateProfileCommand command, CancellationToken cancellationToken = default);

    Task<BrowserProfile> UpdateAsync(UpdateProfileCommand command, CancellationToken cancellationToken = default);

    Task<BrowserProfile> DuplicateConfigurationAsync(DuplicateProfileCommand command, CancellationToken cancellationToken = default);

    Task<BrowserProfile> SetFavoriteAsync(Guid profileId, bool isFavorite, CancellationToken cancellationToken = default);

    Task<BrowserProfile> SetArchivedAsync(Guid profileId, bool isArchived, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid profileId, bool isProfileActive = false, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BrowserProfile>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default);
}
