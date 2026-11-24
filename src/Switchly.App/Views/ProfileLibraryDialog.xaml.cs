using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Switchly.Application.Browser;
using Switchly.Domain.Bookmarks;
using Switchly.Domain.Downloads;
using Switchly.Domain.History;

namespace Switchly.App.Views;

public enum ProfileLibraryTab
{
    Bookmarks = 0,
    History = 1,
    Downloads = 2,
}

/// <summary>Interactive per-profile Bookmark (UI-005), History (UI-006), and Downloads (UI-007) manager.</summary>
public partial class ProfileLibraryDialog : Window
{
    private readonly Guid _profileId;
    private readonly Action<string> _openUrlCallback;

    public ProfileLibraryDialog(Guid profileId, string profileName, ProfileLibraryTab initialTab, Action<string> openUrlCallback)
    {
        InitializeComponent();

        _profileId = profileId;
        _openUrlCallback = openUrlCallback ?? throw new ArgumentNullException(nameof(openUrlCallback));
        Title = $"Perpustakaan — {profileName}";
        LibraryTabs.SelectedIndex = (int)initialTab;

        Loaded += async (_, _) => await LoadAllAsync();
    }

    private async Task LoadAllAsync()
    {
        await RefreshBookmarksAsync();
        await RefreshHistoryAsync();
        await RefreshDownloadsAsync();
    }

    private async Task RefreshBookmarksAsync()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookmarkService>();
        var list = await service.ListAsync(_profileId, BookmarkSearchBox.Text);
        BookmarksList.ItemsSource = list.Where(b => !string.IsNullOrWhiteSpace(b.Url)).ToList();
    }

    private async Task RefreshHistoryAsync()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IHistoryService>();
        var list = await service.ListAsync(_profileId, HistorySearchBox.Text, limit: 200);
        HistoryList.ItemsSource = list;
    }

    private async Task RefreshDownloadsAsync()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDownloadService>();
        var list = await service.ListByProfileAsync(_profileId, limit: 100);
        DownloadsList.ItemsSource = list;
    }

    private async void OnBookmarkSearchChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await RefreshBookmarksAsync();
        }
    }

    private async void OnHistorySearchChanged(object sender, TextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            await RefreshHistoryAsync();
        }
    }

    private void OnOpenBookmarkClick(object sender, RoutedEventArgs e)
        => OpenSelectedBookmark();

    private void OnBookmarkDoubleClick(object sender, MouseButtonEventArgs e)
        => OpenSelectedBookmark();

    private void OpenSelectedBookmark()
    {
        if (BookmarksList.SelectedItem is Bookmark bookmark && !string.IsNullOrWhiteSpace(bookmark.Url))
        {
            _openUrlCallback(bookmark.Url);
            Close();
        }
    }

    private async void OnDeleteBookmarkClick(object sender, RoutedEventArgs e)
    {
        if (BookmarksList.SelectedItem is not Bookmark bookmark)
        {
            return;
        }

        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IBookmarkService>();
        await service.DeleteAsync(_profileId, bookmark.BookmarkId);
        await RefreshBookmarksAsync();
    }

    private void OnOpenHistoryClick(object sender, RoutedEventArgs e)
        => OpenSelectedHistory();

    private void OnHistoryDoubleClick(object sender, MouseButtonEventArgs e)
        => OpenSelectedHistory();

    private void OpenSelectedHistory()
    {
        if (HistoryList.SelectedItem is HistoryEntry entry && !string.IsNullOrWhiteSpace(entry.Url))
        {
            _openUrlCallback(entry.Url);
            Close();
        }
    }

    private async void OnClearHistoryClick(object sender, RoutedEventArgs e)
    {
        var confirm = MessageBox.Show(
            "Hapus seluruh riwayat kunjungan milik profil ini?",
            "Konfirmasi Hapus Riwayat",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IHistoryService>();
        await service.ClearProfileHistoryAsync(_profileId);
        await RefreshHistoryAsync();
    }

    private void OnOpenDownloadFolderClick(object sender, RoutedEventArgs e)
        => OpenSelectedDownloadFolder();

    private void OnDownloadDoubleClick(object sender, MouseButtonEventArgs e)
        => OpenSelectedDownloadFolder();

    private void OpenSelectedDownloadFolder()
    {
        if (DownloadsList.SelectedItem is not DownloadRecord record || string.IsNullOrWhiteSpace(record.TargetPath))
        {
            return;
        }

        if (File.Exists(record.TargetPath))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{record.TargetPath}\"") { UseShellExecute = true });
            return;
        }

        var dir = Path.GetDirectoryName(record.TargetPath);
        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        else
        {
            MessageBox.Show("File atau folder unduhan sudah tidak ditemukan di disk.", "Unduhan", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
