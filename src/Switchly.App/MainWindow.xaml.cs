using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Switchly.App.Navigation;
using Switchly.App.Themes;
using Switchly.App.ViewModels;
using Switchly.App.Views;
using Switchly.Application.Profiles;
using Switchly.Domain.Groups;

namespace Switchly.App;

public partial class MainWindow : Window
{
    private readonly WorkspaceViewModel _viewModel;
    private readonly BrowserWindowCoordinator _windowCoordinator;
    private readonly DispatcherTimer _resourceTimer;
    private BrowserWorkspaceView? _currentDockedWorkspace;
    private bool _isSidebarCollapsed;
    private bool _isVideoFullscreen;
    private WindowStyle _savedWindowStyle = WindowStyle.SingleBorderWindow;
    private WindowState _savedWindowState = WindowState.Normal;
    private ResizeMode _savedResizeMode = ResizeMode.CanResize;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<WorkspaceViewModel>();
        _windowCoordinator = App.Services.GetRequiredService<BrowserWindowCoordinator>();
        DataContext = _viewModel;

        _windowCoordinator.ActiveDockedWorkspaceChanged += OnActiveDockedWorkspaceChanged;
        UpdateThemeToggleIcon();

        _resourceTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _resourceTimer.Tick += (_, _) => _viewModel.RefreshResourceUsage();

        Loaded += async (_, _) =>
        {
            await _viewModel.LoadAsync();
            _resourceTimer.Start();
        };
        Closed += (_, _) =>
        {
            _resourceTimer.Stop();
            _windowCoordinator.ActiveDockedWorkspaceChanged -= OnActiveDockedWorkspaceChanged;
            _windowCoordinator.CloseAllWindows();
        };
    }

    private void OnToggleThemeClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.ToggleTheme();
        UpdateThemeToggleIcon();
    }

    private void UpdateThemeToggleIcon()
    {
        var isDark = ThemeManager.CurrentTheme == AppTheme.Dark;
        var iconKey = isDark ? "IconSun" : "IconMoon";
        if (System.Windows.Application.Current?.TryFindResource(iconKey) is System.Windows.Media.Geometry geometry)
        {
            ThemeIconPath.Data = geometry;
        }

        ThemeLabelText.Text = isDark ? "Terang" : "Gelap";
        ThemeToggleButton.ToolTip = isDark
            ? "Beralih ke Mode Terang (Light)"
            : "Beralih ke Mode Gelap (Dark)";

        if (SettingsLightThemeButton is not null && SettingsDarkThemeButton is not null)
        {
            var activeStyle = (Style)FindResource("NavButtonActiveStyle");
            var normalStyle = (Style)FindResource("NavButtonStyle");
            SettingsLightThemeButton.Style = isDark ? normalStyle : activeStyle;
            SettingsDarkThemeButton.Style = isDark ? activeStyle : normalStyle;
        }
    }

    private void ShowMainRightPanel(UIElement panel)
    {
        DashboardView.Visibility = panel == DashboardView ? Visibility.Visible : Visibility.Collapsed;
        DockedBrowserHost.Visibility = panel == DockedBrowserHost ? Visibility.Visible : Visibility.Collapsed;
        ActivityView.Visibility = panel == ActivityView ? Visibility.Visible : Visibility.Collapsed;
        SettingsView.Visibility = panel == SettingsView ? Visibility.Visible : Visibility.Collapsed;

        if (SidebarActivityButton is not null && SidebarSettingsButton is not null)
        {
            var activeStyle = (Style)FindResource("NavButtonActiveStyle");
            var ghostStyle = (Style)FindResource("GhostButtonStyle");
            SidebarActivityButton.Style = panel == ActivityView ? activeStyle : ghostStyle;
            SidebarSettingsButton.Style = panel == SettingsView ? activeStyle : ghostStyle;
        }
    }

    private void OnOpenActivityViewClick(object sender, RoutedEventArgs e)
    {
        _windowCoordinator.ShowDashboard();
        _viewModel.RefreshResourceUsage();
        SetSidebarCollapsed(false);
        ShowMainRightPanel(ActivityView);
    }

    private void OnOpenSettingsViewClick(object sender, RoutedEventArgs e)
    {
        _windowCoordinator.ShowDashboard();
        SetSidebarCollapsed(false);
        UpdateThemeToggleIcon();
        RefreshSettingsLicenseBadge();
        ShowMainRightPanel(SettingsView);
    }

    private void RefreshSettingsLicenseBadge()
    {
        if (SettingsLicenseBadgeText is null)
        {
            return;
        }

        var entitlement = App.Services.GetRequiredService<Switchly.Application.Entitlements.IFeatureEntitlementService>();
        SettingsLicenseBadgeText.Text = entitlement.GetCurrentEntitlement().DisplayBadge;
    }

    private void OnManageLicenseClick(object sender, RoutedEventArgs e)
    {
        OpenLicenseAccountDialog(initialReason: null);
    }

    private void OpenLicenseAccountDialog(string? initialReason)
    {
        var accountService = App.Services.GetRequiredService<Switchly.Application.Entitlements.LicenseAccountService>();
        var dialog = new LicenseAccountDialog(accountService, initialReason)
        {
            Owner = this,
        };
        dialog.ShowDialog();
        RefreshSettingsLicenseBadge();
    }

    private void OnSelectLightThemeClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.ApplyTheme(AppTheme.Light, persist: true);
        UpdateThemeToggleIcon();
    }

    private void OnSelectDarkThemeClick(object sender, RoutedEventArgs e)
    {
        ThemeManager.ApplyTheme(AppTheme.Dark, persist: true);
        UpdateThemeToggleIcon();
    }

    private void UpdateActiveNav(WorkspaceSection section)
    {
        var activeStyle = (Style)FindResource("NavButtonActiveStyle");
        var normalStyle = (Style)FindResource("NavButtonStyle");

        AllProfilesNavButton.Style = section == WorkspaceSection.AllProfiles ? activeStyle : normalStyle;
        FavoritesNavButton.Style = section == WorkspaceSection.Favorites ? activeStyle : normalStyle;
        ArchivedNavButton.Style = section == WorkspaceSection.Archived ? activeStyle : normalStyle;
    }

    public void FocusQuickSearch()
    {
        if (_isSidebarCollapsed)
        {
            SetSidebarCollapsed(false);
        }

        SidebarSearchBox.Focus();
        SidebarSearchBox.SelectAll();
    }

    public void OpenProfileSwitcher()
    {
        ProfileSwitcherOverlay.Visibility = Visibility.Visible;
        SwitcherSearchBox.Text = string.Empty;
        RefreshSwitcherResults();
        SwitcherSearchBox.Focus();
    }

    private void CloseProfileSwitcher()
    {
        ProfileSwitcherOverlay.Visibility = Visibility.Collapsed;
    }

    private void RefreshSwitcherResults()
    {
        var items = _viewModel.GetSwitcherCandidates(SwitcherSearchBox.Text);
        SwitcherResultsList.ItemsSource = items;
        if (items.Count > 0)
        {
            SwitcherResultsList.SelectedIndex = 0;
        }
    }

    private void SetSidebarCollapsed(bool collapsed)
    {
        _isSidebarCollapsed = collapsed;
        if (_isVideoFullscreen)
        {
            return;
        }

        SidebarPanel.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        SidebarColumn.Width = collapsed ? new GridLength(0) : new GridLength(268);

        // Show compact Profil / Aktivitas buttons in Top Profile Strip only when Sidebar is collapsed
        // to eliminate duplicate navigation controls (Hick's Law / Cognitive Clarity).
        var topQuickNavVisibility = collapsed ? Visibility.Visible : Visibility.Collapsed;
        TopProfilesButton.Visibility = topQuickNavVisibility;
        TopActivityButton.Visibility = topQuickNavVisibility;
    }

    private void OnToggleSidebarClick(object sender, RoutedEventArgs e)
        => SetSidebarCollapsed(!_isSidebarCollapsed);

    private void OnOpenProfileSwitcherClick(object sender, RoutedEventArgs e)
        => OpenProfileSwitcher();

    private void OnProfileSwitcherBackdropMouseDown(object sender, MouseButtonEventArgs e)
        => CloseProfileSwitcher();

    private void OnProfileSwitcherCardMouseDown(object sender, MouseButtonEventArgs e)
        => e.Handled = true;

    private void OnSwitcherSearchTextChanged(object sender, TextChangedEventArgs e)
        => RefreshSwitcherResults();

    private async void OnSwitcherSearchPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseProfileSwitcher();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Down && SwitcherResultsList.Items.Count > 0)
        {
            SwitcherResultsList.SelectedIndex = Math.Min(SwitcherResultsList.Items.Count - 1, SwitcherResultsList.SelectedIndex + 1);
            SwitcherResultsList.ScrollIntoView(SwitcherResultsList.SelectedItem);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Up && SwitcherResultsList.Items.Count > 0)
        {
            SwitcherResultsList.SelectedIndex = Math.Max(0, SwitcherResultsList.SelectedIndex - 1);
            SwitcherResultsList.ScrollIntoView(SwitcherResultsList.SelectedItem);
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && SwitcherResultsList.SelectedItem is ProfileListItemViewModel selected)
        {
            e.Handled = true;
            await ActivateSwitcherItemAsync(selected);
        }
    }

    private async void OnSwitcherListPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseProfileSwitcher();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Enter && SwitcherResultsList.SelectedItem is ProfileListItemViewModel selected)
        {
            e.Handled = true;
            await ActivateSwitcherItemAsync(selected);
        }
    }

    private async void OnSwitcherResultActivate(object sender, MouseButtonEventArgs e)
    {
        if (SwitcherResultsList.SelectedItem is ProfileListItemViewModel selected)
        {
            await ActivateSwitcherItemAsync(selected);
        }
    }

    private async void OnSwitcherRowClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ProfileListItemViewModel item })
        {
            e.Handled = true;
            await ActivateSwitcherItemAsync(item);
        }
    }

    private async Task ActivateSwitcherItemAsync(ProfileListItemViewModel item)
    {
        CloseProfileSwitcher();
        await _viewModel.OpenOrFocusProfileAsync(item.Profile);
    }

    private async void OnSwitcherBrowseAllClick(object sender, RoutedEventArgs e)
    {
        CloseProfileSwitcher();
        _windowCoordinator.ShowDashboard();
        UpdateActiveNav(WorkspaceSection.AllProfiles);
        await _viewModel.SelectSectionAsync(WorkspaceSection.AllProfiles);
    }

    private void OnSwitcherNewProfileClick(object sender, RoutedEventArgs e)
    {
        CloseProfileSwitcher();
        OnNewProfileClick(sender, e);
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && ProfileSwitcherOverlay.Visibility == Visibility.Visible)
        {
            CloseProfileSwitcher();
            e.Handled = true;
            return;
        }

        var ctrl = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var shift = (Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift;

        if (ctrl && shift && e.Key == Key.K)
        {
            if (ProfileSwitcherOverlay.Visibility == Visibility.Visible)
            {
                CloseProfileSwitcher();
            }
            else
            {
                OpenProfileSwitcher();
            }

            e.Handled = true;
            return;
        }

        if (ctrl && !shift && e.Key == Key.B && DockedBrowserHost.Visibility == Visibility.Visible)
        {
            SetSidebarCollapsed(!_isSidebarCollapsed);
            e.Handled = true;
        }
    }

    private void OnCardMoreClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is not Button button)
        {
            return;
        }

        DependencyObject? current = button;
        while (current is not null)
        {
            if (current is Border border && border.ContextMenu is not null)
            {
                border.ContextMenu.PlacementTarget = button;
                border.ContextMenu.IsOpen = true;
                return;
            }

            current = System.Windows.Media.VisualTreeHelper.GetParent(current);
        }
    }

    private async void OnProfileCardClick(object sender, MouseButtonEventArgs e)
    {
        // Ignore clicks originating from action buttons or selection checkboxes inside the row.
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null && source != sender)
        {
            if (source is Button or CheckBox)
            {
                return;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        if (sender is FrameworkElement element && element.Tag is ProfileListItemViewModel item)
        {
            await _viewModel.OpenOrFocusProfileAsync(item.Profile);
        }
    }

    private void UpdateStatusFilterButtons(ProfileStatusFilter filter)
    {
        var activeStyle = (Style)FindResource("NavButtonActiveStyle");
        var normalStyle = (Style)FindResource("NavButtonStyle");

        StatusAllButton.Style = filter == ProfileStatusFilter.All ? activeStyle : normalStyle;
        StatusRunningButton.Style = filter == ProfileStatusFilter.Running ? activeStyle : normalStyle;
        StatusInactiveButton.Style = filter == ProfileStatusFilter.Inactive ? activeStyle : normalStyle;
    }

    private void OnStatusFilterAllClick(object sender, RoutedEventArgs e)
    {
        UpdateStatusFilterButtons(ProfileStatusFilter.All);
        _viewModel.StatusFilter = ProfileStatusFilter.All;
        SelectAllProfilesCheckBox.IsChecked = false;
    }

    private void OnStatusFilterRunningClick(object sender, RoutedEventArgs e)
    {
        UpdateStatusFilterButtons(ProfileStatusFilter.Running);
        _viewModel.StatusFilter = ProfileStatusFilter.Running;
        SelectAllProfilesCheckBox.IsChecked = false;
    }

    private void OnStatusFilterInactiveClick(object sender, RoutedEventArgs e)
    {
        UpdateStatusFilterButtons(ProfileStatusFilter.Inactive);
        _viewModel.StatusFilter = ProfileStatusFilter.Inactive;
        SelectAllProfilesCheckBox.IsChecked = false;
    }

    private void OnSortOrderSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_viewModel is null || sender is not ComboBox { SelectedItem: ComboBoxItem { Tag: string tag } })
        {
            return;
        }

        if (Enum.TryParse<ProfileSortOrder>(tag, out var sortOrder))
        {
            _viewModel.SortOrder = sortOrder;
        }
    }

    private void OnSelectAllProfilesClick(object sender, RoutedEventArgs e)
    {
        var isChecked = SelectAllProfilesCheckBox.IsChecked == true;
        _viewModel.SetAllVisibleSelected(isChecked);
    }

    private void OnRowSelectionCheckboxClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _viewModel.NotifySelectionChanged();
        SelectAllProfilesCheckBox.IsChecked =
            _viewModel.VisibleProfiles.Count > 0 &&
            _viewModel.SelectedProfileCount == _viewModel.VisibleProfiles.Count;
    }

    private void OnBulkClearSelectionClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SetAllVisibleSelected(false);
        SelectAllProfilesCheckBox.IsChecked = false;
    }

    private async void OnBulkCloseSessionsClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.CloseSelectedSessionsAsync();
        SelectAllProfilesCheckBox.IsChecked = false;
    }

    private async void OnBulkArchiveClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.ArchiveSelectedProfilesAsync();
        SelectAllProfilesCheckBox.IsChecked = false;
    }

    private async void OnBulkDeleteClick(object sender, RoutedEventArgs e)
    {
        var count = _viewModel.SelectedProfileCount;
        if (count == 0)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Hapus {count} profil terpilih secara permanen?\n\nSemua data sesi browser (cookies, cache, localStorage) milik profil yang tidak aktif akan dihapus. Profil yang masih berjalan akan dilewati dengan aman.",
            "Konfirmasi Hapus Massal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            await _viewModel.DeleteSelectedProfilesAsync();
            SelectAllProfilesCheckBox.IsChecked = false;
        }
    }

    private static ProfileGroup? ExtractGroup(object sender)
    {
        if (sender is FrameworkElement { Tag: ManualGroupItemViewModel item })
        {
            return item.Group;
        }

        if (sender is FrameworkElement { Tag: ProfileGroup group })
        {
            return group;
        }

        return null;
    }

    private async void OnGroupRowMouseUp(object sender, MouseButtonEventArgs e)
    {
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null && source != sender)
        {
            if (source is Button)
            {
                return;
            }

            source = System.Windows.Media.VisualTreeHelper.GetParent(source);
        }

        if (ExtractGroup(sender) is { } group)
        {
            _windowCoordinator.ShowDashboard();
            ShowMainRightPanel(DashboardView);
            UpdateActiveNav(WorkspaceSection.Group);
            await _viewModel.SelectSectionAsync(WorkspaceSection.Group, group);
        }
    }

    private async void OnCategoryChipClick(object sender, RoutedEventArgs e)
    {
        if (ExtractGroup(sender) is { } group)
        {
            _windowCoordinator.ShowDashboard();
            ShowMainRightPanel(DashboardView);
            UpdateActiveNav(WorkspaceSection.Group);
            await _viewModel.SelectSectionAsync(WorkspaceSection.Group, group);
        }
    }

    private void OnActiveDockedWorkspaceChanged(BrowserWorkspaceView? workspace)
    {
        Dispatcher.Invoke(() =>
        {
            if (_currentDockedWorkspace is not null)
            {
                _currentDockedWorkspace.FullscreenModeChanged -= OnWorkspaceFullscreenModeChanged;
            }

            if (_isVideoFullscreen)
            {
                OnWorkspaceFullscreenModeChanged(false);
            }

            _currentDockedWorkspace = workspace;
            if (_currentDockedWorkspace is not null)
            {
                _currentDockedWorkspace.FullscreenModeChanged += OnWorkspaceFullscreenModeChanged;
            }

            _viewModel.SetActiveDockedProfile(workspace?.ProfileId);
            DockedBrowserHost.Content = workspace;
            ShowMainRightPanel(workspace is not null ? DockedBrowserHost : DashboardView);

            // Browser-first (D14): collapse sidebar automatically while browsing so the viewport is maximized;
            // restore sidebar when returning to the Profile Library / Dashboard.
            SetSidebarCollapsed(workspace is not null);
        });
    }

    private void OnWorkspaceFullscreenModeChanged(bool isFullscreen)
    {
        if (_isVideoFullscreen == isFullscreen)
        {
            return;
        }

        _isVideoFullscreen = isFullscreen;
        if (isFullscreen)
        {
            _savedWindowStyle = WindowStyle;
            _savedWindowState = WindowState;
            _savedResizeMode = ResizeMode;

            SidebarPanel.Visibility = Visibility.Collapsed;
            SidebarColumn.Width = new GridLength(0);
            TopProfileStripBar.Visibility = Visibility.Collapsed;
            FooterStatusBar.Visibility = Visibility.Collapsed;
            ProfileSwitcherOverlay.Visibility = Visibility.Collapsed;

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
        else
        {
            SidebarPanel.Visibility = _isSidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
            SidebarColumn.Width = _isSidebarCollapsed ? new GridLength(0) : new GridLength(268);
            TopProfileStripBar.Visibility = Visibility.Visible;
            FooterStatusBar.Visibility = Visibility.Visible;

            WindowStyle = _savedWindowStyle;
            ResizeMode = _savedResizeMode;
            WindowState = _savedWindowState;
        }
    }

    private async void OnAllProfilesClick(object sender, RoutedEventArgs e)
    {
        _windowCoordinator.ShowDashboard();
        ShowMainRightPanel(DashboardView);
        UpdateActiveNav(WorkspaceSection.AllProfiles);
        await _viewModel.SelectSectionAsync(WorkspaceSection.AllProfiles);
    }

    private async void OnFavoritesClick(object sender, RoutedEventArgs e)
    {
        _windowCoordinator.ShowDashboard();
        ShowMainRightPanel(DashboardView);
        UpdateActiveNav(WorkspaceSection.Favorites);
        await _viewModel.SelectSectionAsync(WorkspaceSection.Favorites);
    }

    private async void OnArchivedClick(object sender, RoutedEventArgs e)
    {
        _windowCoordinator.ShowDashboard();
        ShowMainRightPanel(DashboardView);
        UpdateActiveNav(WorkspaceSection.Archived);
        await _viewModel.SelectSectionAsync(WorkspaceSection.Archived);
    }

    private async void OnGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is ProfileGroup group)
        {
            _windowCoordinator.ShowDashboard();
            UpdateActiveNav(WorkspaceSection.Group);
            await _viewModel.SelectSectionAsync(WorkspaceSection.Group, group);
        }
    }

    private async void OnNewProfileClick(object sender, RoutedEventArgs e)
    {
        var entitlement = App.Services.GetRequiredService<Switchly.Application.Entitlements.IFeatureEntitlementService>();
        var createDecision = entitlement.CanCreateProfile(_viewModel.TotalSavedProfileCount);
        if (!createDecision.IsAllowed)
        {
            OpenLicenseAccountDialog(createDecision.Reason);
            return;
        }

        IReadOnlyCollection<Guid>? preselectedGroupIds = _viewModel.SelectedGroup is { } activeGroup
            ? [activeGroup.GroupId]
            : null;

        var dialog = new ProfileEditorDialog(
            existing: null,
            availableManualGroups: _viewModel.ManualGroups.ToList(),
            currentManualGroupIds: preselectedGroupIds)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.CreateProfileAsync(
                new CreateProfileCommand(
                    Name: dialog.ProfileName,
                    StartupUrl: dialog.StartupUrl,
                    ColorHex: dialog.ColorHex,
                    IsFavorite: dialog.IsFavorite,
                    RestoreLastSession: dialog.RestoreLastSession,
                    PersistSessionCookies: dialog.PersistSessionCookies),
                manualGroupIds: dialog.SelectedManualGroupIds);
        }
    }

    private async void OnEditProfileClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is not { } item)
        {
            return;
        }

        var currentGroupIds = await _viewModel.GetManualGroupIdsForProfileAsync(item.ProfileId);
        var dialog = new ProfileEditorDialog(
            existing: item.Profile,
            availableManualGroups: _viewModel.ManualGroups.ToList(),
            currentManualGroupIds: currentGroupIds)
        {
            Owner = this,
        };

        if (dialog.ShowDialog() == true)
        {
            await _viewModel.UpdateProfileAsync(
                new UpdateProfileCommand(
                    ProfileId: item.ProfileId,
                    Name: dialog.ProfileName,
                    StartupUrl: dialog.StartupUrl,
                    ColorHex: dialog.ColorHex,
                    IconKey: item.Profile.IconKey,
                    IsFavorite: dialog.IsFavorite,
                    RestoreLastSession: dialog.RestoreLastSession,
                    PersistSessionCookies: dialog.PersistSessionCookies),
                manualGroupIds: dialog.SelectedManualGroupIds);
        }
    }

    private async void OnDeleteManualGroupClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (ExtractGroup(sender) is { } group)
        {
            var confirm = MessageBox.Show(
                $"Hapus grup '{group.Name}'? Profil di dalamnya tidak akan terhapus.",
                "Hapus Grup",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                await _viewModel.DeleteManualGroupAsync(group);
            }
        }
    }

    private async void OnOpenProfileClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is { } item)
        {
            await _viewModel.OpenOrFocusProfileAsync(item.Profile);
        }
    }

    private async void OnOpenNewWindowClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is { } item)
        {
            await _viewModel.OpenNewWindowAsync(item.Profile);
        }
    }

    private void OnActiveWorkspaceClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ActiveWorkspaceItemViewModel item })
        {
            _viewModel.FocusActiveWorkspace(item);
        }
    }

    private async void OnCloseActiveWorkspaceClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (sender is FrameworkElement { Tag: ActiveWorkspaceItemViewModel item })
        {
            await _viewModel.CloseActiveWorkspaceAsync(item);
        }
    }

    private async void OnCloseAllSessionsForActiveTaskProfileClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ActiveWorkspaceItemViewModel item })
        {
            await _viewModel.CloseProfileSessionAsync(item.ProfileId);
            _viewModel.RefreshResourceUsage();
        }
    }

    private async void OnRenameManualGroupClick(object sender, RoutedEventArgs e)
    {
        if (ExtractGroup(sender) is { } group)
        {
            var dialog = new GroupEditorDialog(group)
            {
                Owner = this,
            };
            if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.GroupName))
            {
                await _viewModel.UpdateManualGroupAsync(group, dialog.GroupName, dialog.DefaultUrl);
            }
        }
    }

    private async void OnCloseGroupSessionsClick(object sender, RoutedEventArgs e)
    {
        if (ExtractGroup(sender) is { } group)
        {
            await _viewModel.CloseGroupWorkspacesAsync(group);
        }
    }

    private async void OnCloseSessionClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        if (ExtractItem(sender) is { } item)
        {
            await _viewModel.CloseProfileSessionAsync(item.ProfileId);
            _viewModel.RefreshResourceUsage();
        }
    }

    private async void OnCloseBackgroundSessionsClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.CloseBackgroundSessionsAsync();
    }

    private async void OnCloseAllSessionsClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.ActiveProfileCount > 1)
        {
            var confirm = MessageBox.Show(
                $"Tutup seluruh {_viewModel.ActiveProfileCount} sesi profil yang sedang aktif?\n\nData login & cookie tetap aman tersimpan di masing-masing profil.",
                "Tutup Semua Sesi",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }
        }

        await _viewModel.CloseAllSessionsAsync();
    }

    private void OnGroupChipMoreClick(object sender, RoutedEventArgs e)
        => OnCardMoreClick(sender, e);

    private void OnDismissMemoryWarningClick(object sender, RoutedEventArgs e)
        => _viewModel.DismissMemoryWarning();

    private void OnToggleAutoSleepClick(object sender, RoutedEventArgs e)
    {
        _viewModel.ToggleAutoSleep();
    }

    private async void OnToggleFavoriteClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is { } item)
        {
            await _viewModel.ToggleFavoriteAsync(item.Profile);
        }
    }

    private async void OnDuplicateProfileClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is { } item)
        {
            await _viewModel.DuplicateProfileAsync(item.ProfileId);
        }
    }

    private async void OnToggleArchiveClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is { } item)
        {
            await _viewModel.ToggleArchiveAsync(item.Profile);
        }
    }

    private async void OnDeleteProfileClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is not { } item)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Hapus profil '{item.Name}' secara permanen?\n\nSemua data sesi browser (cookies, cache, localStorage) milik profil ini akan ikut dihapus.",
            "Konfirmasi Hapus Profil",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            await _viewModel.DeleteProfileAsync(item.Profile);
        }
    }

    private void OnImportBrowserClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ImportWizardDialog { Owner = this };
        dialog.ShowDialog();
    }

    private void OnSecurityAndBackupClick(object sender, RoutedEventArgs e)
    {
        var dialog = new SecurityAndBackupDialog { Owner = this };
        dialog.ShowDialog();
    }

    private void OnImportForProfileClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is { } item)
        {
            var dialog = new ImportWizardDialog(item.ProfileId) { Owner = this };
            dialog.ShowDialog();
        }
    }

    private async void OnCreateGroupClick(object sender, RoutedEventArgs e)
    {
        var dialog = new GroupEditorDialog { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.GroupName))
        {
            await _viewModel.CreateManualGroupAsync(dialog.GroupName, dialog.DefaultUrl);
        }
    }

    private async void OnHideGroupClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ProfileGroup group })
        {
            await _viewModel.HideGroupAsync(group);
        }
    }

    private static ProfileListItemViewModel? ExtractItem(object sender)
        => sender is FrameworkElement { Tag: ProfileListItemViewModel item } ? item : null;
}
