using System.Text;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Switchly.Application.Import;
using Switchly.Application.Profiles;
using Switchly.Domain.Import;
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>
/// Read-only Import Wizard dialog for Chrome and Edge profiles (UI-009, REQ-008, REQ-009).
/// </summary>
public partial class ImportWizardDialog : Window
{
    private readonly Guid? _preselectedTargetProfileId;
    private bool _isBusy;

    public ImportWizardDialog(Guid? preselectedTargetProfileId = null)
    {
        InitializeComponent();
        _preselectedTargetProfileId = preselectedTargetProfileId;

        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        LoadDetectedProfiles();

        using var scope = App.Services.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        var profiles = await profileService.ListAsync(includeArchived: false);
        TargetProfileCombo.ItemsSource = profiles;
        if (_preselectedTargetProfileId.HasValue)
        {
            TargetProfileCombo.SelectedItem = profiles.FirstOrDefault(p => p.ProfileId == _preselectedTargetProfileId.Value)
                ?? profiles.FirstOrDefault();
        }
        else
        {
            TargetProfileCombo.SelectedItem = profiles.FirstOrDefault();
        }
    }

    private void OnBrowserFilterChanged(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        LoadDetectedProfiles();
    }

    private void OnRefreshSourcesClick(object sender, RoutedEventArgs e)
        => LoadDetectedProfiles();

    private void LoadDetectedProfiles()
    {
        var filter = EdgeRadio.IsChecked == true ? ImportSourceBrowser.Edge : ImportSourceBrowser.Chrome;
        var discovery = App.Services.GetRequiredService<IBrowserProfileDiscovery>();
        var detected = discovery.DiscoverLocalProfiles(filter);
        SourceProfileCombo.ItemsSource = detected;
        SourceProfileCombo.SelectedItem = detected.FirstOrDefault();
        SourceEmptyHint.Visibility = detected.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ExecuteButton.IsEnabled = false;
        SummaryCard.Visibility = Visibility.Collapsed;
        LockedWarningBanner.Visibility = Visibility.Collapsed;
    }

    private async void OnPreviewClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        if (!TryGetInputs(out var source, out var target, out var categories))
        {
            return;
        }

        SetBusy(true, "Memindai data sumber (baca-saja)...");
        try
        {
            var preview = await Task.Run(async () =>
            {
                using var scope = App.Services.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<IBrowserImportService>();
                return await importService.PreviewAsync(source, categories, target.ProfileId);
            });
            LockedWarningBanner.Visibility = preview.IsHistoryDatabaseLocked ? Visibility.Visible : Visibility.Collapsed;

            var sb = new StringBuilder();
            if (categories.HasFlag(ImportCategories.Bookmarks))
            {
                sb.AppendLine($"• Bookmark ditemukan: {preview.BookmarkCount} (duplikat dilewati: {preview.BookmarkDuplicateCount})");
            }

            if (categories.HasFlag(ImportCategories.History))
            {
                sb.AppendLine($"• Riwayat ditemukan: {preview.HistoryCount} (duplikat dilewati: {preview.HistoryDuplicateCount})");
            }

            if (categories.HasFlag(ImportCategories.OpenTabs))
            {
                sb.AppendLine($"• Tab terbuka (URL) ditemukan: {preview.OpenTabCount}");
            }

            SummaryHeaderText.Text = $"Pratinjau Impor → {target.Name}";
            SummaryBodyText.Text = sb.ToString().TrimEnd();
            WarningsList.ItemsSource = preview.Warnings;
            SummaryCard.Visibility = Visibility.Visible;
            ExecuteButton.IsEnabled = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal membuat pratinjau impor: {ex.Message}", "Impor Browser", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, string.Empty);
        }
    }

    private async void OnExecuteClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        if (!TryGetInputs(out var source, out var target, out var categories))
        {
            return;
        }

        SetBusy(true, "Mengimpor data ke profil Switchly Next...");
        try
        {
            var result = await Task.Run(async () =>
            {
                using var scope = App.Services.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<IBrowserImportService>();
                return await importService.ExecuteAsync(source, categories, target.ProfileId);
            });
            LockedWarningBanner.Visibility = result.IsHistoryDatabaseLocked ? Visibility.Visible : Visibility.Collapsed;

            var sb = new StringBuilder();
            sb.AppendLine($"Status: {FormatStatus(result.Status)}");
            if (categories.HasFlag(ImportCategories.Bookmarks))
            {
                sb.AppendLine($"• Bookmark: {result.BookmarksImported} berhasil, {result.BookmarksSkipped} dilewati");
            }

            if (categories.HasFlag(ImportCategories.History))
            {
                sb.AppendLine($"• Riwayat: {result.HistoryImported} berhasil, {result.HistorySkipped} dilewati");
            }

            if (categories.HasFlag(ImportCategories.OpenTabs))
            {
                sb.AppendLine($"• Tab Terbuka: {result.OpenTabsImported} berhasil, {result.OpenTabsSkipped} dilewati");
            }

            SummaryHeaderText.Text = $"Hasil Impor → {target.Name}";
            SummaryBodyText.Text = sb.ToString().TrimEnd();
            WarningsList.ItemsSource = result.Warnings;
            SummaryCard.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Gagal menjalankan impor: {ex.Message}", "Impor Browser", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false, string.Empty);
        }
    }

    private bool TryGetInputs(out DetectedBrowserProfile source, out BrowserProfile target, out ImportCategories categories)
    {
        source = (SourceProfileCombo.SelectedItem as DetectedBrowserProfile)!;
        target = (TargetProfileCombo.SelectedItem as BrowserProfile)!;
        categories = ImportCategories.None;

        if (source is null)
        {
            MessageBox.Show("Pilih profil sumber Chrome atau Edge terlebih dahulu.", "Impor Browser", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (target is null)
        {
            MessageBox.Show("Buat dan pilih profil tujuan Switchly Next terlebih dahulu.", "Impor Browser", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        if (BookmarksCheck.IsChecked == true)
        {
            categories |= ImportCategories.Bookmarks;
        }

        if (HistoryCheck.IsChecked == true)
        {
            categories |= ImportCategories.History;
        }

        if (OpenTabsCheck.IsChecked == true)
        {
            categories |= ImportCategories.OpenTabs;
        }

        if (categories == ImportCategories.None)
        {
            MessageBox.Show("Pilih minimal satu kategori data untuk diimpor.", "Impor Browser", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        return true;
    }

    private void SetBusy(bool isBusy, string statusMessage)
    {
        _isBusy = isBusy;
        PreviewButton.IsEnabled = !isBusy;
        ExecuteButton.IsEnabled = !isBusy && SummaryCard.Visibility == Visibility.Visible;
        BusyStatusText.Text = statusMessage;
        BusyStatusText.Visibility = isBusy ? Visibility.Visible : Visibility.Collapsed;
    }

    private static string FormatStatus(ImportJobStatus status) => status switch
    {
        ImportJobStatus.Completed => "Selesai",
        ImportJobStatus.Partial => "Selesai Sebagian (lihat peringatan)",
        ImportJobStatus.Failed => "Gagal (lihat peringatan)",
        ImportJobStatus.Cancelled => "Dibatalkan",
        _ => status.ToString(),
    };
}
