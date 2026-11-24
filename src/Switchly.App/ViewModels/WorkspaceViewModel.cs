using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchly.App.Common;
using Switchly.App.Navigation;
using Switchly.Application.Browser;
using Switchly.Application.Groups;
using Switchly.Application.Profiles;
using Switchly.Domain.Groups;
using Switchly.Domain.Profiles;

namespace Switchly.App.ViewModels;

public enum WorkspaceSection
{
    AllProfiles,
    Favorites,
    Archived,
    Group,
}

/// <summary>Coordinates Main Workspace state: profiles, manual/auto groups, search/filter, and live status (UI-001).</summary>
public sealed class WorkspaceViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProfileSessionManager _sessionManager;
    private readonly BrowserWindowCoordinator _windowCoordinator;
    private readonly ILogger<WorkspaceViewModel> _logger;

    private List<BrowserProfile> _allProfiles = [];
    private WorkspaceSection _activeSection = WorkspaceSection.AllProfiles;
    private ProfileGroup? _selectedGroup;
    private string _searchText = string.Empty;
    private string? _errorMessage;
    private string _sectionTitle = "Semua Profil";
    private int _totalSavedCount;
    private int _activeProfileCount;
    private bool _isEmpty;

    public WorkspaceViewModel(
        IServiceScopeFactory scopeFactory,
        IProfileSessionManager sessionManager,
        BrowserWindowCoordinator windowCoordinator,
        ILogger<WorkspaceViewModel> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _windowCoordinator = windowCoordinator ?? throw new ArgumentNullException(nameof(windowCoordinator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _sessionManager.StateChanged += OnSessionStateChanged;
        _windowCoordinator.GroupsChanged += () => _ = RefreshGroupsAsync();
    }

    public ObservableCollection<ProfileListItemViewModel> VisibleProfiles { get; } = [];

    public ObservableCollection<ProfileGroup> ManualGroups { get; } = [];

    public ObservableCollection<ProfileGroup> WebsiteGroups { get; } = [];

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                _ = ApplyFilterAsync();
            }
        }
    }

    public string SectionTitle
    {
        get => _sectionTitle;
        private set => SetProperty(ref _sectionTitle, value);
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetProperty(ref _errorMessage, value))
            {
                OnPropertyChanged(nameof(HasError));
            }
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsEmpty
    {
        get => _isEmpty;
        private set => SetProperty(ref _isEmpty, value);
    }

    public string ResourceStatusText => $"Profil tersimpan: {_totalSavedCount}  •  Aktif: {_activeProfileCount}";

    public async Task LoadAsync()
    {
        ErrorMessage = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();

            _allProfiles = [.. await profileService.ListAsync(includeArchived: true)];
            _totalSavedCount = _allProfiles.Count(p => !p.IsArchived);
            _activeProfileCount = _sessionManager.GetActiveSessions().Count;
            OnPropertyChanged(nameof(ResourceStatusText));

            await ReloadGroupCollectionsAsync(groupService);
            await ApplyFilterAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load workspace data.");
            ErrorMessage = $"Gagal memuat workspace: {ex.Message}";
        }
    }

    public async Task SelectSectionAsync(WorkspaceSection section, ProfileGroup? group = null)
    {
        _activeSection = section;
        _selectedGroup = group;
        SectionTitle = section switch
        {
            WorkspaceSection.Favorites => "Favorit",
            WorkspaceSection.Archived => "Arsip",
            WorkspaceSection.Group when group is not null => group.Kind == GroupKind.WebsiteDomain
                ? $"Website: {group.Name}"
                : $"Grup: {group.Name}",
            _ => "Semua Profil",
        };

        await ApplyFilterAsync();
    }

    public async Task CreateProfileAsync(CreateProfileCommand command)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        var created = await profileService.CreateAsync(command);

        if (!string.IsNullOrWhiteSpace(created.StartupUrl))
        {
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
            await groupService.SyncWebsiteDomainGroupAsync(created.ProfileId, created.StartupUrl);
        }

        await LoadAsync();
    }

    public async Task UpdateProfileAsync(UpdateProfileCommand command)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        var updated = await profileService.UpdateAsync(command);

        if (!string.IsNullOrWhiteSpace(updated.StartupUrl))
        {
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
            await groupService.SyncWebsiteDomainGroupAsync(updated.ProfileId, updated.StartupUrl);
        }

        await LoadAsync();
    }

    public async Task DuplicateProfileAsync(Guid profileId)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        await profileService.DuplicateConfigurationAsync(new DuplicateProfileCommand(profileId));
        await LoadAsync();
    }

    public async Task ToggleFavoriteAsync(BrowserProfile profile)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        await profileService.SetFavoriteAsync(profile.ProfileId, !profile.IsFavorite);
        await LoadAsync();
    }

    public async Task ToggleArchiveAsync(BrowserProfile profile)
    {
        ErrorMessage = null;
        if (!profile.IsArchived && _sessionManager.GetState(profile.ProfileId) == ProfileRuntimeState.Active)
        {
            await _windowCoordinator.CloseProfileWindowsAsync(profile.ProfileId);
        }

        using var scope = _scopeFactory.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        await profileService.SetArchivedAsync(profile.ProfileId, !profile.IsArchived);
        await LoadAsync();
    }

    public async Task DeleteProfileAsync(BrowserProfile profile)
    {
        ErrorMessage = null;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
            var isActive = _sessionManager.GetState(profile.ProfileId) == ProfileRuntimeState.Active;
            await profileService.DeleteAsync(profile.ProfileId, isActive);
            await LoadAsync();
        }
        catch (ProfileInUseException)
        {
            ErrorMessage = $"Profil '{profile.Name}' sedang aktif. Tutup sesinya terlebih dahulu sebelum menghapus.";
        }
    }

    public async Task OpenOrFocusProfileAsync(BrowserProfile profile)
    {
        ErrorMessage = null;
        try
        {
            await _windowCoordinator.OpenOrFocusProfileAsync(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open profile {ProfileId}.", profile.ProfileId);
            ErrorMessage = $"Gagal membuka profil '{profile.Name}': {ex.Message}";
        }
    }

    public async Task OpenNewWindowAsync(BrowserProfile profile)
    {
        ErrorMessage = null;
        try
        {
            await _windowCoordinator.OpenNewWindowForProfileAsync(profile);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to open new window for profile {ProfileId}.", profile.ProfileId);
            ErrorMessage = $"Gagal membuka jendela baru: {ex.Message}";
        }
    }

    public async Task CloseProfileSessionAsync(Guid profileId)
    {
        ErrorMessage = null;
        await _windowCoordinator.CloseProfileWindowsAsync(profileId);
    }

    public async Task CreateManualGroupAsync(string name)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
        await groupService.CreateManualGroupAsync(name);
        await RefreshGroupsAsync();
    }

    public async Task HideGroupAsync(ProfileGroup group)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
        await groupService.SetGroupHiddenAsync(group.GroupId, true);
        if (_selectedGroup?.GroupId == group.GroupId)
        {
            await SelectSectionAsync(WorkspaceSection.AllProfiles);
        }

        await RefreshGroupsAsync();
    }

    private async Task RefreshGroupsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(
                async () => await ReloadGroupCollectionsAsync(groupService));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh groups.");
        }
    }

    private async Task ReloadGroupCollectionsAsync(IGroupService groupService)
    {
        var groups = await groupService.ListGroupsAsync(includeHidden: false);
        ManualGroups.Clear();
        WebsiteGroups.Clear();

        foreach (var group in groups)
        {
            if (group.Kind == GroupKind.Manual)
            {
                ManualGroups.Add(group);
            }
            else
            {
                WebsiteGroups.Add(group);
            }
        }
    }

    private async Task ApplyFilterAsync()
    {
        IReadOnlySet<Guid>? allowedIds = null;
        if (_activeSection == WorkspaceSection.Group && _selectedGroup is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
            var ids = await groupService.GetProfileIdsInGroupAsync(_selectedGroup.GroupId);
            allowedIds = ids.ToHashSet();
        }

        var criteria = new ProfileFilterCriteria(
            SearchText: _searchText,
            FavoritesOnly: _activeSection == WorkspaceSection.Favorites,
            ArchivedOnly: _activeSection == WorkspaceSection.Archived,
            AllowedProfileIds: allowedIds);

        var filtered = ProfileFilter.Apply(_allProfiles, criteria);
        var activeMap = _sessionManager.GetActiveSessions().ToDictionary(s => s.ProfileId, s => s.OpenWindowCount);

        VisibleProfiles.Clear();
        foreach (var profile in filtered)
        {
            var state = _sessionManager.GetState(profile.ProfileId);
            var windowCount = activeMap.TryGetValue(profile.ProfileId, out var c) ? c : 0;
            VisibleProfiles.Add(new ProfileListItemViewModel(profile, state, windowCount));
        }

        IsEmpty = VisibleProfiles.Count == 0;
    }

    private void OnSessionStateChanged(ProfileSessionSnapshot snapshot)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            var item = VisibleProfiles.FirstOrDefault(p => p.ProfileId == snapshot.ProfileId);
            if (item is not null)
            {
                item.RuntimeState = snapshot.State;
                item.OpenWindowCount = snapshot.OpenWindowCount;
            }

            _activeProfileCount = _sessionManager.GetActiveSessions().Count;
            OnPropertyChanged(nameof(ResourceStatusText));
        });
    }
}
