using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Switchly.App.Navigation;
using Switchly.App.ViewModels;
using Switchly.App.Views;
using Switchly.Application.Profiles;
using Switchly.Domain.Groups;

namespace Switchly.App;

public partial class MainWindow : Window
{
    private readonly WorkspaceViewModel _viewModel;
    private readonly BrowserWindowCoordinator _windowCoordinator;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = App.Services.GetRequiredService<WorkspaceViewModel>();
        _windowCoordinator = App.Services.GetRequiredService<BrowserWindowCoordinator>();
        DataContext = _viewModel;

        Loaded += async (_, _) => await _viewModel.LoadAsync();
        Closed += (_, _) => _windowCoordinator.CloseAllWindows();
    }

    private async void OnAllProfilesClick(object sender, RoutedEventArgs e)
        => await _viewModel.SelectSectionAsync(WorkspaceSection.AllProfiles);

    private async void OnFavoritesClick(object sender, RoutedEventArgs e)
        => await _viewModel.SelectSectionAsync(WorkspaceSection.Favorites);

    private async void OnArchivedClick(object sender, RoutedEventArgs e)
        => await _viewModel.SelectSectionAsync(WorkspaceSection.Archived);

    private async void OnGroupSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is ListBox listBox && listBox.SelectedItem is ProfileGroup group)
        {
            await _viewModel.SelectSectionAsync(WorkspaceSection.Group, group);
        }
    }

    private async void OnNewProfileClick(object sender, RoutedEventArgs e)
    {
        var dialog = new ProfileEditorDialog { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await _viewModel.CreateProfileAsync(new CreateProfileCommand(
                Name: dialog.ProfileName,
                StartupUrl: dialog.StartupUrl,
                ColorHex: dialog.ColorHex,
                IsFavorite: dialog.IsFavorite,
                RestoreLastSession: dialog.RestoreLastSession,
                PersistSessionCookies: dialog.PersistSessionCookies));
        }
    }

    private async void OnEditProfileClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is not { } item)
        {
            return;
        }

        var dialog = new ProfileEditorDialog(item.Profile) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            await _viewModel.UpdateProfileAsync(new UpdateProfileCommand(
                ProfileId: item.ProfileId,
                Name: dialog.ProfileName,
                StartupUrl: dialog.StartupUrl,
                ColorHex: dialog.ColorHex,
                IconKey: item.Profile.IconKey,
                IsFavorite: dialog.IsFavorite,
                RestoreLastSession: dialog.RestoreLastSession,
                PersistSessionCookies: dialog.PersistSessionCookies));
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

    private async void OnCloseSessionClick(object sender, RoutedEventArgs e)
    {
        if (ExtractItem(sender) is { } item)
        {
            await _viewModel.CloseProfileSessionAsync(item.ProfileId);
        }
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

    private async void OnCreateGroupClick(object sender, RoutedEventArgs e)
    {
        var dialog = new TextInputDialog("Grup Baru", "Masukkan nama grup manual:") { Owner = this };
        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.InputText))
        {
            await _viewModel.CreateManualGroupAsync(dialog.InputText);
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
