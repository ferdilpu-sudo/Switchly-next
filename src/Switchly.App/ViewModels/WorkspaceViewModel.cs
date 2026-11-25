using System.Collections.ObjectModel;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Switchly.App.Common;
using Switchly.App.Navigation;
using Switchly.Application.Browser;
using Switchly.Application.Diagnostics;
using Switchly.Application.Groups;
using Switchly.Application.Profiles;
using Switchly.Domain.Diagnostics;
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

public sealed record ActiveWorkspaceItemViewModel(
    Guid ProfileId,
    Guid? WorkspaceGroupId,
    string ProfileName,
    string? WorkspaceGroupName,
    string ColorHex,
    bool IsCurrent)
{
    public string InitialLetter => string.IsNullOrWhiteSpace(ProfileName)
        ? "?"
        : ProfileName.Trim()[0].ToString().ToUpperInvariant();

    public string DisplayTitle => string.IsNullOrWhiteSpace(WorkspaceGroupName)
        ? ProfileName
        : $"{WorkspaceGroupName}  •  {ProfileName}";

    public string Subtitle => string.IsNullOrWhiteSpace(WorkspaceGroupName)
        ? "Browser Umum"
        : $"Profil: {ProfileName}";
}

public sealed record ActiveWorkspaceGroupViewModel(
    string GroupTitle,
    IReadOnlyList<ActiveWorkspaceItemViewModel> Items)
{
    public string HeaderLabel => $"{GroupTitle} ({Items.Count})";
}

/// <summary>Coordinates Main Workspace state: profiles, manual/auto groups, search/filter, and live status (UI-001).</summary>
public sealed class WorkspaceViewModel : ViewModelBase
{
    private static readonly TimeSpan IdleAutoSleepThreshold = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IProfileSessionManager _sessionManager;
    private readonly BrowserWindowCoordinator _windowCoordinator;
    private readonly IResourceMonitor _resourceMonitor;
    private readonly ILogger<WorkspaceViewModel> _logger;
    private readonly Dictionary<Guid, DateTimeOffset> _profileLastActiveUtc = [];
    private readonly string _autoSleepPreferencePath;
    private bool _autoSleepEnabled = true;
    private bool _autoSleepSweepRunning;

    private List<BrowserProfile> _allProfiles = [];
    private Dictionary<Guid, string> _profileManualGroupMap = [];
    private WorkspaceSection _activeSection = WorkspaceSection.AllProfiles;
    private ProfileGroup? _selectedGroup;
    private Guid? _activeDockedProfileId;
    private string _searchText = string.Empty;
    private string? _errorMessage;
    private string? _memoryWarningMessage;
    private string _resourceStatusText = "Profil tersimpan: 0  •  Aktif: 0";
    private string _sectionTitle = "Semua Profil";
    private string _sectionSubtitle = "Klik kartu profil untuk membuka browser terisolasi. Atur grup dari menu Ubah Profil.";
    private int _totalSavedCount;
    private int _activeProfileCount;
    private bool _isEmpty;

    public WorkspaceViewModel(
        IServiceScopeFactory scopeFactory,
        IProfileSessionManager sessionManager,
        BrowserWindowCoordinator windowCoordinator,
        IResourceMonitor resourceMonitor,
        ILogger<WorkspaceViewModel> logger)
    {
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _sessionManager = sessionManager ?? throw new ArgumentNullException(nameof(sessionManager));
        _windowCoordinator = windowCoordinator ?? throw new ArgumentNullException(nameof(windowCoordinator));
        _resourceMonitor = resourceMonitor ?? throw new ArgumentNullException(nameof(resourceMonitor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _sessionManager.StateChanged += OnSessionStateChanged;
        _windowCoordinator.GroupsChanged += () => _ = RefreshGroupsAsync();
        _windowCoordinator.DockedWorkspacesChanged += RefreshActiveWorkspaces;

        var dataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwitchlyNext",
            "Data");
        _autoSleepPreferencePath = Path.Combine(dataDir, "ui-autosleep.txt");
        LoadAutoSleepPreference();
    }

    public ObservableCollection<ProfileListItemViewModel> VisibleProfiles { get; } = [];

    public ObservableCollection<ActiveWorkspaceItemViewModel> ActiveWorkspaces { get; } = [];

    public ObservableCollection<ActiveWorkspaceGroupViewModel> ActiveWorkspaceGroups { get; } = [];

    public bool HasActiveWorkspaces => ActiveWorkspaces.Count > 0;

    public void FocusActiveWorkspace(ActiveWorkspaceItemViewModel item)
        => _windowCoordinator.FocusDockedWorkspace(item.ProfileId, item.WorkspaceGroupId);

    public async Task CloseActiveWorkspaceAsync(ActiveWorkspaceItemViewModel item)
    {
        await _windowCoordinator.CloseDockedWorkspaceAsync(item.ProfileId, item.WorkspaceGroupId);
        RefreshResourceUsage();
    }

    private void RefreshActiveWorkspaces()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.Invoke(() =>
        {
            ActiveWorkspaces.Clear();
            ActiveWorkspaceGroups.Clear();

            var items = _windowCoordinator.GetOpenDockedWorkspaces()
                .Select(info => new ActiveWorkspaceItemViewModel(
                    info.ProfileId,
                    info.WorkspaceGroupId,
                    info.ProfileName,
                    info.WorkspaceGroupName,
                    info.ColorHex,
                    info.IsCurrent))
                .ToList();

            foreach (var item in items)
            {
                ActiveWorkspaces.Add(item);
            }

            foreach (var bucket in items.GroupBy(i => string.IsNullOrWhiteSpace(i.WorkspaceGroupName) ? "Browser Umum" : i.WorkspaceGroupName!))
            {
                ActiveWorkspaceGroups.Add(new ActiveWorkspaceGroupViewModel(bucket.Key, bucket.ToList()));
            }

            OnPropertyChanged(nameof(HasActiveWorkspaces));
        });
    }

    public ObservableCollection<ProfileGroup> ManualGroups { get; } = [];

    public ObservableCollection<ProfileGroup> WebsiteGroups { get; } = [];

    public bool HasWebsiteGroups => WebsiteGroups.Count > 0;

    public bool HasManualGroups => ManualGroups.Count > 0;

    public bool HasActiveSessions => _activeProfileCount > 0;

    public bool HasBackgroundSessions =>
        _activeDockedProfileId.HasValue ? _activeProfileCount > 1 : _activeProfileCount > 0;

    public int ActiveProfileCount => _activeProfileCount;

    public bool AutoSleepEnabled
    {
        get => _autoSleepEnabled;
        private set
        {
            if (SetProperty(ref _autoSleepEnabled, value))
            {
                OnPropertyChanged(nameof(AutoSleepLabel));
            }
        }
    }

    public string AutoSleepLabel => AutoSleepEnabled ? "Auto-Sleep 30m: ON" : "Auto-Sleep: OFF";

    public void ToggleAutoSleep()
    {
        AutoSleepEnabled = !AutoSleepEnabled;
        try
        {
            var dir = Path.GetDirectoryName(_autoSleepPreferencePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllText(_autoSleepPreferencePath, AutoSleepEnabled ? "ON" : "OFF");
        }
        catch
        {
            // Ignore non-critical UI preference write failures.
        }
    }

    public void SetActiveDockedProfile(Guid? profileId)
    {
        var now = DateTimeOffset.UtcNow;
        if (_activeDockedProfileId.HasValue)
        {
            _profileLastActiveUtc[_activeDockedProfileId.Value] = now;
        }

        _activeDockedProfileId = profileId;
        if (profileId.HasValue)
        {
            _profileLastActiveUtc[profileId.Value] = now;
        }

        foreach (var item in VisibleProfiles)
        {
            item.IsCurrentlyDocked = profileId.HasValue && item.ProfileId == profileId.Value;
        }

        OnPropertyChanged(nameof(HasBackgroundSessions));
    }

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

    public string SectionSubtitle
    {
        get => _sectionSubtitle;
        private set => SetProperty(ref _sectionSubtitle, value);
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

    public string? MemoryWarningMessage
    {
        get => _memoryWarningMessage;
        private set
        {
            if (SetProperty(ref _memoryWarningMessage, value))
            {
                OnPropertyChanged(nameof(HasMemoryWarning));
            }
        }
    }

    public bool HasMemoryWarning => !string.IsNullOrWhiteSpace(MemoryWarningMessage);

    public string ResourceStatusText
    {
        get => _resourceStatusText;
        private set => SetProperty(ref _resourceStatusText, value);
    }

    public void RefreshResourceUsage()
    {
        var activeSessions = _sessionManager.GetActiveSessions();
        _activeProfileCount = activeSessions.Count;
        var now = DateTimeOffset.UtcNow;
        foreach (var session in activeSessions)
        {
            if (!_profileLastActiveUtc.ContainsKey(session.ProfileId) || session.ProfileId == _activeDockedProfileId)
            {
                _profileLastActiveUtc[session.ProfileId] = now;
            }
        }

        var snapshot = _resourceMonitor.CaptureSnapshot(_totalSavedCount, _activeProfileCount);
        var assessment = ResourcePressureEvaluator.Evaluate(snapshot);
        ResourceStatusText = assessment.StatusSummary;
        MemoryWarningMessage = assessment.NonModalWarningMessage;
        OnPropertyChanged(nameof(HasActiveSessions));
        OnPropertyChanged(nameof(HasBackgroundSessions));

        if (AutoSleepEnabled && !_autoSleepSweepRunning)
        {
            _ = TryAutoSleepIdleProfilesAsync();
        }
    }

    public async Task CloseBackgroundSessionsAsync()
    {
        ErrorMessage = null;
        var activeIds = _sessionManager.GetActiveSessions()
            .Select(s => s.ProfileId)
            .Where(id => !_activeDockedProfileId.HasValue || id != _activeDockedProfileId.Value)
            .ToList();

        foreach (var id in activeIds)
        {
            await _windowCoordinator.CloseProfileWindowsAsync(id);
            _profileLastActiveUtc.Remove(id);
        }

        RefreshResourceUsage();
    }

    public async Task CloseAllSessionsAsync()
    {
        ErrorMessage = null;
        var activeIds = _sessionManager.GetActiveSessions()
            .Select(s => s.ProfileId)
            .ToList();

        foreach (var id in activeIds)
        {
            await _windowCoordinator.CloseProfileWindowsAsync(id);
            _profileLastActiveUtc.Remove(id);
        }

        RefreshResourceUsage();
    }

    private async Task TryAutoSleepIdleProfilesAsync()
    {
        _autoSleepSweepRunning = true;
        try
        {
            var now = DateTimeOffset.UtcNow;
            var idleCandidates = _sessionManager.GetActiveSessions()
                .Select(s => s.ProfileId)
                .Where(id =>
                    (!_activeDockedProfileId.HasValue || id != _activeDockedProfileId.Value) &&
                    !_windowCoordinator.HasOpenPopoutWindow(id) &&
                    _profileLastActiveUtc.TryGetValue(id, out var lastUtc) &&
                    now - lastUtc >= IdleAutoSleepThreshold)
                .ToList();

            foreach (var id in idleCandidates)
            {
                _logger.LogInformation("Auto-sleeping idle background profile {ProfileId} after {Minutes} minutes.", id, IdleAutoSleepThreshold.TotalMinutes);
                await _windowCoordinator.CloseProfileWindowsAsync(id);
                _profileLastActiveUtc.Remove(id);
            }

            if (idleCandidates.Count > 0)
            {
                RefreshResourceUsage();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Auto-sleep sweep encountered an error.");
        }
        finally
        {
            _autoSleepSweepRunning = false;
        }
    }

    private void LoadAutoSleepPreference()
    {
        try
        {
            if (File.Exists(_autoSleepPreferencePath))
            {
                var raw = File.ReadAllText(_autoSleepPreferencePath).Trim();
                _autoSleepEnabled = !string.Equals(raw, "OFF", StringComparison.OrdinalIgnoreCase);
            }
        }
        catch
        {
            _autoSleepEnabled = true;
        }
    }

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
            RefreshResourceUsage();

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
            WorkspaceSection.Group when group is not null => !string.IsNullOrWhiteSpace(group.DefaultUrl)
                ? $"Workspace: {group.Name}"
                : $"Grup: {group.Name}",
            _ => "Semua Profil",
        };

        SectionSubtitle = section switch
        {
            WorkspaceSection.Favorites => "Profil favorit Anda untuk akses cepat.",
            WorkspaceSection.Archived => "Profil yang diarsipkan. Klik kanan untuk memulihkan.",
            WorkspaceSection.Group when group is not null && !string.IsNullOrWhiteSpace(group.DefaultUrl) =>
                $"Klik kartu profil mana saja untuk langsung membuka {group.DefaultUrl}",
            WorkspaceSection.Group when group is not null =>
                $"Menampilkan profil di dalam folder '{group.Name}'.",
            _ => "Klik kartu profil untuk membuka browser terisolasi. Atur grup dari menu Ubah Profil.",
        };

        await ApplyFilterAsync();
    }

    public async Task CreateProfileAsync(CreateProfileCommand command, IReadOnlyList<Guid>? manualGroupIds = null)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        var created = await profileService.CreateAsync(command);

        if (manualGroupIds is { Count: > 0 })
        {
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
            foreach (var groupId in manualGroupIds.Distinct())
            {
                await groupService.AssignProfileToManualGroupAsync(created.ProfileId, groupId);
            }
        }

        await LoadAsync();
    }

    public async Task UpdateProfileAsync(UpdateProfileCommand command, IReadOnlyList<Guid>? manualGroupIds = null)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        var updated = await profileService.UpdateAsync(command);
        var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();

        var desiredSet = manualGroupIds?.ToHashSet() ?? [];
        var existingGroups = await groupService.GetGroupsForProfileAsync(updated.ProfileId, includeHidden: true);
        var existingManualIds = existingGroups
            .Where(g => g.Kind == GroupKind.Manual)
            .Select(g => g.GroupId)
            .ToHashSet();

        foreach (var toRemove in existingManualIds.Except(desiredSet))
        {
            await groupService.RemoveProfileFromManualGroupAsync(updated.ProfileId, toRemove);
        }

        foreach (var toAdd in desiredSet.Except(existingManualIds))
        {
            await groupService.AssignProfileToManualGroupAsync(updated.ProfileId, toAdd);
        }

        await LoadAsync();
    }

    public async Task<IReadOnlyList<Guid>> GetManualGroupIdsForProfileAsync(Guid profileId)
    {
        using var scope = _scopeFactory.CreateScope();
        var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
        var groups = await groupService.GetGroupsForProfileAsync(profileId, includeHidden: false);
        return groups.Where(g => g.Kind == GroupKind.Manual).Select(g => g.GroupId).ToList();
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
            var activeGroup = _activeSection == WorkspaceSection.Group ? _selectedGroup : null;
            await _windowCoordinator.OpenOrFocusProfileAsync(
                profile,
                workspaceGroupId: activeGroup?.GroupId,
                workspaceGroupName: activeGroup?.Name,
                targetUrl: activeGroup?.DefaultUrl);
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
            var targetUrl = _activeSection == WorkspaceSection.Group ? _selectedGroup?.DefaultUrl : null;
            await _windowCoordinator.OpenNewWindowForProfileAsync(profile, targetUrl);
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

    public async Task CreateManualGroupAsync(string name, string? defaultUrl = null)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
        await groupService.CreateManualGroupAsync(name, defaultUrl);
        await RefreshGroupsAsync();
    }

    public async Task RenameManualGroupAsync(ProfileGroup group, string newName)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
        var updated = await groupService.RenameManualGroupAsync(group.GroupId, newName);
        if (_selectedGroup?.GroupId == group.GroupId)
        {
            await SelectSectionAsync(WorkspaceSection.Group, updated);
        }

        await LoadAsync();
    }

    public async Task DeleteManualGroupAsync(ProfileGroup group)
    {
        ErrorMessage = null;
        using var scope = _scopeFactory.CreateScope();
        var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
        await groupService.DeleteManualGroupAsync(group.GroupId);
        if (_selectedGroup?.GroupId == group.GroupId)
        {
            await SelectSectionAsync(WorkspaceSection.AllProfiles);
        }

        await LoadAsync();
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
        var groupNamesByProfile = new Dictionary<Guid, List<string>>();

        foreach (var group in groups)
        {
            if (group.Kind == GroupKind.Manual)
            {
                ManualGroups.Add(group);
                var memberIds = await groupService.GetProfileIdsInGroupAsync(group.GroupId);
                foreach (var profileId in memberIds)
                {
                    if (!groupNamesByProfile.TryGetValue(profileId, out var list))
                    {
                        list = [];
                        groupNamesByProfile[profileId] = list;
                    }

                    list.Add(group.Name);
                }
            }
            else
            {
                WebsiteGroups.Add(group);
            }
        }

        _profileManualGroupMap = groupNamesByProfile.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Count <= 2
                ? string.Join(", ", kvp.Value)
                : $"{kvp.Value[0]}, {kvp.Value[1]} +{kvp.Value.Count - 2}");
        OnPropertyChanged(nameof(HasWebsiteGroups));
        OnPropertyChanged(nameof(HasManualGroups));
    }

    private async Task ApplyFilterAsync()
    {
        IReadOnlySet<Guid>? allowedIds = null;
        if (_activeSection == WorkspaceSection.Group && _selectedGroup is not null)
        {
            using var scope = _scopeFactory.CreateScope();
            var groupService = scope.ServiceProvider.GetRequiredService<IGroupService>();
            var ids = await groupService.GetProfileIdsInGroupAsync(_selectedGroup.GroupId);

            // D10 (Opsi A): When a Web Workspace Group has a DefaultUrl and no explicitly assigned members,
            // show all non-archived profiles so any profile can launch straight into that group's web workspace.
            if (ids.Count > 0 || string.IsNullOrWhiteSpace(_selectedGroup.DefaultUrl))
            {
                allowedIds = ids.ToHashSet();
            }
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
            var isDocked = _activeDockedProfileId.HasValue && profile.ProfileId == _activeDockedProfileId.Value;
            _profileManualGroupMap.TryGetValue(profile.ProfileId, out var groupName);
            VisibleProfiles.Add(new ProfileListItemViewModel(profile, state, windowCount, isDocked, groupName));
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
            OnPropertyChanged(nameof(HasActiveSessions));
            OnPropertyChanged(nameof(HasBackgroundSessions));
        });
    }
}
