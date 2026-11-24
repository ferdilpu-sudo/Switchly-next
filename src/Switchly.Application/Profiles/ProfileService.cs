using Switchly.Domain.Profiles;

namespace Switchly.Application.Profiles;

/// <summary>Implements profile CRUD, configuration-only duplication, favorite, archive, and safe deletion.</summary>
public sealed class ProfileService : IProfileService
{
    private readonly IProfileRepository _repository;
    private readonly IProfileStorageDirectory _storage;
    private readonly IProfileLockManager? _lockManager;
    private readonly TimeProvider _timeProvider;

    public ProfileService(
        IProfileRepository repository,
        IProfileStorageDirectory storage,
        IProfileLockManager? lockManager = null,
        TimeProvider? timeProvider = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _lockManager = lockManager;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<BrowserProfile> CreateAsync(CreateProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var profileId = Guid.NewGuid();
        var now = _timeProvider.GetUtcNow();
        var storageRelativePath = ProfileValidation.GenerateStorageRelativePath(profileId);

        var profile = new BrowserProfile
        {
            ProfileId = profileId,
            Name = ProfileValidation.NormalizeAndValidateName(command.Name),
            StartupUrl = ProfileValidation.NormalizeAndValidateStartupUrl(command.StartupUrl),
            ColorHex = ProfileValidation.NormalizeAndValidateColorHex(command.ColorHex),
            IconKey = ProfileValidation.NormalizeAndValidateIconKey(command.IconKey),
            StorageRelativePath = storageRelativePath,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            IsFavorite = command.IsFavorite,
            IsArchived = false,
            RestoreLastSession = command.RestoreLastSession,
            PersistSessionCookies = command.PersistSessionCookies,
        };

        _storage.EnsureCreated(storageRelativePath);
        try
        {
            await _repository.AddAsync(profile, cancellationToken);
        }
        catch
        {
            _storage.DeleteIfExists(storageRelativePath);
            throw;
        }

        return profile;
    }

    public async Task<BrowserProfile> UpdateAsync(UpdateProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var existing = await _repository.GetByIdAsync(command.ProfileId, cancellationToken)
            ?? throw new ProfileNotFoundException(command.ProfileId);

        existing.Name = ProfileValidation.NormalizeAndValidateName(command.Name);
        existing.StartupUrl = ProfileValidation.NormalizeAndValidateStartupUrl(command.StartupUrl);
        existing.ColorHex = ProfileValidation.NormalizeAndValidateColorHex(command.ColorHex);
        existing.IconKey = ProfileValidation.NormalizeAndValidateIconKey(command.IconKey);
        existing.IsFavorite = command.IsFavorite;
        existing.RestoreLastSession = command.RestoreLastSession;
        existing.PersistSessionCookies = command.PersistSessionCookies;
        existing.UpdatedAtUtc = _timeProvider.GetUtcNow();

        await _repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    /// <summary>
    /// Duplicates non-secret profile configuration only. Never copies browser storage, cookies, or session data (REQ-002, AC-002).
    /// </summary>
    public async Task<BrowserProfile> DuplicateConfigurationAsync(DuplicateProfileCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var source = await _repository.GetByIdAsync(command.SourceProfileId, cancellationToken)
            ?? throw new ProfileNotFoundException(command.SourceProfileId);

        var duplicateName = string.IsNullOrWhiteSpace(command.NewName)
            ? BuildDefaultCopyName(source.Name)
            : command.NewName;

        return await CreateAsync(
            new CreateProfileCommand(
                Name: duplicateName,
                StartupUrl: source.StartupUrl,
                ColorHex: source.ColorHex,
                IconKey: source.IconKey,
                IsFavorite: false,
                RestoreLastSession: source.RestoreLastSession,
                PersistSessionCookies: source.PersistSessionCookies),
            cancellationToken);
    }

    public async Task<BrowserProfile> SetFavoriteAsync(Guid profileId, bool isFavorite, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(profileId, cancellationToken)
            ?? throw new ProfileNotFoundException(profileId);

        existing.IsFavorite = isFavorite;
        existing.UpdatedAtUtc = _timeProvider.GetUtcNow();
        await _repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task<BrowserProfile> SetArchivedAsync(Guid profileId, bool isArchived, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByIdAsync(profileId, cancellationToken)
            ?? throw new ProfileNotFoundException(profileId);

        existing.IsArchived = isArchived;
        existing.UpdatedAtUtc = _timeProvider.GetUtcNow();
        await _repository.UpdateAsync(existing, cancellationToken);
        return existing;
    }

    public async Task DeleteAsync(Guid profileId, bool isProfileActive = false, CancellationToken cancellationToken = default)
    {
        if (isProfileActive || (_lockManager?.IsLocked(profileId) ?? false))
        {
            throw new ProfileInUseException(profileId);
        }

        var existing = await _repository.GetByIdAsync(profileId, cancellationToken)
            ?? throw new ProfileNotFoundException(profileId);

        var storagePath = existing.StorageRelativePath;
        await _repository.DeleteAsync(existing, cancellationToken);
        _storage.DeleteIfExists(storagePath);
    }

    public Task<IReadOnlyList<BrowserProfile>> ListAsync(bool includeArchived = false, CancellationToken cancellationToken = default)
        => _repository.GetAllAsync(includeArchived, cancellationToken);

    private static string BuildDefaultCopyName(string sourceName)
    {
        const string suffix = " (Copy)";
        var maxPrefix = ProfileValidation.MaxNameLength - suffix.Length;
        var prefix = sourceName.Length > maxPrefix ? sourceName[..maxPrefix].TrimEnd() : sourceName;
        return prefix + suffix;
    }
}
