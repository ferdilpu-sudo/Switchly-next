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

        ThemeToggleButton.ToolTip = isDark
            ? "Beralih ke Mode Terang (Light)"
            : "Beralih ke Mode Gelap (Dark)";
    }

    private void UpdateActiveNav(WorkspaceSection section)
    {
        var activeStyle = (Style)FindResource("NavButtonActiveStyle");
        var normalStyle = (Style)FindResource("NavButtonStyle");

        AllProfilesNavButton.Style = section == WorkspaceSection.AllProfiles ? activeStyle : normalStyle;
        FavoritesNavButton.Style = section == WorkspaceSection.Favorites ? activeStyle : normalStyle;
        ArchivedNavButton.Style = section == WorkspaceSection.Archived ? activeStyle : normalStyle;
    }

    private void OnWindowPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.K && (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
        {
            SidebarSearchBox.Focus();
            SidebarSearchBox.SelectAll();
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
        // Ignore clicks originating from action buttons inside the card.
        DependencyObject? source = e.OriginalSource as DependencyObject;
        while (source is not null && source != sender)
        {
            if (source is Button)
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

    private async void OnCategoryChipClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement element && element.Tag is ProfileGroup group)
        {
            _windowCoordinator.ShowDashboard();
            UpdateActiveNav(WorkspaceSection.Group);
            await _viewModel.SelectSectionAsync(WorkspaceSection.Group, group);
        }
    }

    private void OnActiveDockedWorkspaceChanged(BrowserWorkspaceView? workspace)
    {
        Dispatcher.Invoke(() =>
        {
            _viewModel.SetActiveDockedProfile(workspace?.ProfileId);
            DockedBrowserHost.Content = workspace;
            DockedBrowserHost.Visibility = workspace is not null ? Visibility.Visible : Visibility.Collapsed;
            DashboardView.Visibility = workspace is null ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private async void OnAllProfilesClick(object sender, RoutedEventArgs e)
    {
        _windowCoordinator.ShowDashboard();
        UpdateActiveNav(WorkspaceSection.AllProfiles);
        await _viewModel.SelectSectionAsync(WorkspaceSection.AllProfiles);
    }

    private async void OnFavoritesClick(object sender, RoutedEventArgs e)
    {
        _windowCoordinator.ShowDashboard();
        UpdateActiveNav(WorkspaceSection.Favorites);
        await _viewModel.SelectSectionAsync(WorkspaceSection.Favorites);
    }

    private async void OnArchivedClick(object sender, RoutedEventArgs e)
    {
        _windowCoordinator.ShowDashboard();
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
        var dialog = new ProfileEditorDialog(
            existing: null,
            availableManualGroups: _viewModel.ManualGroups.ToList())
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
        if (sender is FrameworkElement element && element.Tag is ProfileGroup group)
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
