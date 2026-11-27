using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Switchly.App.ViewModels;
using Switchly.Application.Browser;
using Switchly.Domain.Bookmarks;
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
        var items = list.Where(b => !string.IsNullOrWhiteSpace(b.Url)).ToList();
        BookmarksList.ItemsSource = items;
        DeleteBookmarkButton.IsEnabled = items.Count > 0;
    }

    private async Task RefreshHistoryAsync()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IHistoryService>();
        var list = await service.ListAsync(_profileId, HistorySearchBox.Text, limit: 200);
        HistoryList.ItemsSource = list;
        ClearHistoryButton.IsEnabled = list.Count > 0;
    }

    private async Task RefreshDownloadsAsync()
    {
        using var scope = App.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IDownloadService>();
        var list = await service.ListByProfileAsync(_profileId, limit: 100);
        DownloadsList.ItemsSource = list.Select(DownloadItemViewModel.FromRecord).ToList();
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

        var confirm = MessageBox.Show(
            $"Hapus bookmark '{bookmark.Title}' dari profil ini?",
            "Konfirmasi Hapus Bookmark",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
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
        var count = HistoryList.Items.Count;
        if (count == 0)
        {
            return;
        }

        var confirm = MessageBox.Show(
            $"Hapus seluruh riwayat kunjungan ({count} entri) milik profil ini?",
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

    private void OnOpenDownloadFileClick(object sender, RoutedEventArgs e)
    {
        if (DownloadsList.SelectedItem is DownloadItemViewModel item)
        {
            DownloadItemViewModel.OpenDownloadedFile(item.TargetPath);
        }
    }

    private void OnOpenDownloadFolderClick(object sender, RoutedEventArgs e)
        => OpenSelectedDownloadFolder();

    private void OnDownloadDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DownloadsList.SelectedItem is DownloadItemViewModel item)
        {
            if (item.IsCompleted)
            {
                DownloadItemViewModel.OpenDownloadedFile(item.TargetPath);
            }
            else
            {
                DownloadItemViewModel.ShowInExplorer(item.TargetPath);
            }
        }
    }

    private void OpenSelectedDownloadFolder()
    {
        if (DownloadsList.SelectedItem is DownloadItemViewModel item)
        {
            DownloadItemViewModel.ShowInExplorer(item.TargetPath);
        }
    }

    private void OnCopyBookmarkUrlClick(object sender, RoutedEventArgs e)
    {
        if (BookmarksList.SelectedItem is Bookmark bookmark && !string.IsNullOrWhiteSpace(bookmark.Url))
        {
            TryCopyToClipboard(bookmark.Url);
        }
    }

    private void OnCopyHistoryUrlClick(object sender, RoutedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryEntry entry && !string.IsNullOrWhiteSpace(entry.Url))
        {
            TryCopyToClipboard(entry.Url);
        }
    }

    private void OnCopyDownloadPathClick(object sender, RoutedEventArgs e)
    {
        if (DownloadsList.SelectedItem is DownloadItemViewModel item && !string.IsNullOrWhiteSpace(item.TargetPath))
        {
            TryCopyToClipboard(item.TargetPath);
        }
    }

    private static void TryCopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
        }
    }
}
