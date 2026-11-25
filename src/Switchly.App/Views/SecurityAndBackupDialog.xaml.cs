using System.Diagnostics;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;
using Switchly.App.Navigation;
using Switchly.Application.Profiles;
using Switchly.Application.Security;
using Switchly.Application.Vault;
using Switchly.Domain.Profiles;

namespace Switchly.App.Views;

/// <summary>Local Security (UI-010) and Manual Backup/Restore (UI-011) dialog.</summary>
public partial class SecurityAndBackupDialog : Window
{
    private readonly ILocalSecurityService _securityService;
    private readonly BrowserWindowCoordinator _windowCoordinator;
    private CancellationTokenSource? _opCts;

    public SecurityAndBackupDialog()
    {
        InitializeComponent();

        _securityService = App.Services.GetRequiredService<ILocalSecurityService>();
        _windowCoordinator = App.Services.GetRequiredService<BrowserWindowCoordinator>();
        Loaded += async (_, _) =>
        {
            RefreshSecurityStatus();
            await LoadVaultProfilesAsync();
        };
    }

    private void RefreshSecurityStatus()
    {
        var status = _securityService.GetStatus();
        DpapiStatusText.Text = status.IsProtectionHealthy
            ? "Status DPAPI (CurrentUser): Aktif & Sehat"
            : $"Status DPAPI: Bermasalah ({status.ErrorMessage})";
        HelloStatusText.Text = status.IsWindowsHelloAvailable
            ? "Windows Hello: Tersedia"
            : "Windows Hello: Tidak tersedia pada build Win32 unpackaged saat ini (gunakan akun Windows + kata sandi utama opsional).";

        EnableMasterPasswordPanel.Visibility = status.IsMasterPasswordEnabled ? Visibility.Collapsed : Visibility.Visible;
        DisableMasterPasswordPanel.Visibility = status.IsMasterPasswordEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnEnableMasterPasswordClick(object sender, RoutedEventArgs e)
    {
        var p1 = NewMasterPasswordBox.Password;
        var p2 = ConfirmMasterPasswordBox.Password;
        if (!string.Equals(p1, p2, StringComparison.Ordinal))
        {
            SecurityFeedbackText.Text = "Konfirmasi kata sandi utama tidak cocok.";
            return;
        }

        try
        {
            _securityService.EnableMasterPassword(p1);
            NewMasterPasswordBox.Clear();
            ConfirmMasterPasswordBox.Clear();
            SecurityFeedbackText.Text = "Kunci kata sandi utama berhasil diaktifkan.";
            RefreshSecurityStatus();
        }
        catch (Exception ex)
        {
            SecurityFeedbackText.Text = ex.Message;
        }
    }

    private void OnDisableMasterPasswordClick(object sender, RoutedEventArgs e)
    {
        var current = CurrentMasterPasswordBox.Password;
        if (_securityService.DisableMasterPassword(current))
        {
            CurrentMasterPasswordBox.Clear();
            SecurityFeedbackText.Text = "Kunci kata sandi utama dinonaktifkan.";
            RefreshSecurityStatus();
        }
        else
        {
            SecurityFeedbackText.Text = "Kata sandi utama saat ini salah.";
            CurrentMasterPasswordBox.Clear();
        }
    }

    private async void OnCreateBackupClick(object sender, RoutedEventArgs e)
    {
        var pass = BackupPassphraseBox.Password;
        var confirm = BackupPassphraseConfirmBox.Password;
        if (string.IsNullOrEmpty(pass) || pass.Length < 8)
        {
            BackupStatusText.Text = "Passphrase minimal 8 karakter.";
            return;
        }

        if (!string.Equals(pass, confirm, StringComparison.Ordinal))
        {
            BackupStatusText.Text = "Konfirmasi passphrase tidak cocok.";
            return;
        }

        var dlg = new SaveFileDialog
        {
            Title = "Simpan Arsip Backup Switchly",
            Filter = "Switchly Backup (*.snbak)|*.snbak",
            FileName = $"switchly-backup-{DateTime.Now:yyyyMMdd-HHmm}.snbak",
        };
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        // Close open browser windows before backup so profile storage is flushed cleanly (design.md §4.5).
        _windowCoordinator.CloseAllWindows();

        await RunWithProgressAsync(async (progress, ct) =>
        {
            using var scope = App.Services.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupRestoreService>();
            var result = await backupService.CreateBackupAsync(dlg.FileName, pass, progress, ct);
            Dispatcher.Invoke(() =>
            {
                BackupPassphraseBox.Clear();
                BackupPassphraseConfirmBox.Clear();
                BackupStatusText.Text =
                    $"Backup berhasil dibuat ({result.ProfileCount} profil, {result.ArchiveSizeBytes / 1024.0:F1} KB).";
            });
        });
    }

    private void OnBrowseRestoreArchiveClick(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "Pilih Arsip Backup Switchly",
            Filter = "Switchly Backup (*.snbak)|*.snbak|Semua File (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        RestoreArchivePathBox.Text = dlg.FileName;
        try
        {
            using var scope = App.Services.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupRestoreService>();
            var manifest = backupService.InspectBackup(dlg.FileName);
            RestorePreviewText.Text =
                $"Versi arsip: v{manifest.ArchiveVersion}\n" +
                $"Tanggal dibuat: {manifest.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}\n" +
                $"Jumlah profil: {manifest.ProfileCount}\n" +
                $"Perkiraan ukuran data: {manifest.EstimatedPlaintextBytes / 1024.0:F1} KB\n" +
                "Catatan: Cookies/sesi login tidak dibawa lintas komputer (Anda perlu login ulang).";
            RestorePreviewCard.Visibility = Visibility.Visible;
            BackupStatusText.Text = string.Empty;
        }
        catch (Exception ex)
        {
            RestorePreviewCard.Visibility = Visibility.Collapsed;
            BackupStatusText.Text = $"Arsip tidak dapat dibaca: {ex.Message}";
        }
    }

    private async void OnStageRestoreClick(object sender, RoutedEventArgs e)
    {
        var archivePath = RestoreArchivePathBox.Text;
        var pass = RestorePassphraseBox.Password;
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            BackupStatusText.Text = "Pilih file arsip .snbak terlebih dahulu.";
            return;
        }

        if (string.IsNullOrEmpty(pass))
        {
            BackupStatusText.Text = "Masukkan passphrase arsip backup.";
            return;
        }

        var confirm = MessageBox.Show(
            "Pemulihan akan memverifikasi arsip ke folder staging, lalu menggantikan data lokal saat aplikasi dimulai ulang (restart-to-restore).\n\nLanjutkan?",
            "Konfirmasi Pemulihan Backup",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        await RunWithProgressAsync(async (progress, ct) =>
        {
            using var scope = App.Services.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupRestoreService>();
            var staged = await backupService.StageRestoreAsync(archivePath, pass, progress, ct);
            Dispatcher.Invoke(() =>
            {
                RestorePassphraseBox.Clear();
                BackupStatusText.Text =
                    $"Staging selesai ({staged.ExtractedFileCount} file terverifikasi). Aplikasi perlu dimulai ulang untuk menyelesaikan pemulihan.";

                var restart = MessageBox.Show(
                    "Pemulihan telah diverifikasi dan siap diterapkan. Mulai ulang Switchly sekarang?",
                    "Restart untuk Pemulihan",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information);
                if (restart == MessageBoxResult.Yes)
                {
                    var exePath = Environment.ProcessPath;
                    if (!string.IsNullOrEmpty(exePath))
                    {
                        Process.Start(new ProcessStartInfo(exePath) { UseShellExecute = true });
                    }

                    System.Windows.Application.Current.Shutdown(0);
                }
            });
        });
    }

    private void OnCancelOperationClick(object sender, RoutedEventArgs e)
        => _opCts?.Cancel();

    private async Task LoadVaultProfilesAsync()
    {
        using var scope = App.Services.CreateScope();
        var profileService = scope.ServiceProvider.GetRequiredService<IProfileService>();
        var profiles = await profileService.ListAsync(includeArchived: false);
        VaultProfileCombo.ItemsSource = profiles;
        VaultProfileCombo.SelectedItem = profiles.FirstOrDefault();
    }

    private async void OnVaultProfileChanged(object sender, SelectionChangedEventArgs e)
        => await RefreshVaultCredentialsAsync();

    private async Task RefreshVaultCredentialsAsync()
    {
        if (VaultProfileCombo.SelectedItem is not BrowserProfile profile)
        {
            VaultCredentialsList.ItemsSource = null;
            return;
        }

        try
        {
            using var scope = App.Services.CreateScope();
            var vault = scope.ServiceProvider.GetRequiredService<IPasswordVaultService>();
            var items = await vault.ListSummariesAsync(profile.ProfileId);
            VaultCredentialsList.ItemsSource = items;
        }
        catch (Exception ex)
        {
            VaultFeedbackText.Text = $"Gagal memuat Password Vault: {ex.Message}";
        }
    }

    private async void OnImportCsvPasswordsClick(object sender, RoutedEventArgs e)
    {
        if (VaultProfileCombo.SelectedItem is not BrowserProfile profile)
        {
            VaultFeedbackText.Text = "Pilih profil Switchly terlebih dahulu.";
            return;
        }

        var dlg = new OpenFileDialog
        {
            Title = "Pilih File CSV Password (Chrome / Edge)",
            Filter = "CSV Files (*.csv)|*.csv|Semua File (*.*)|*.*",
        };
        if (dlg.ShowDialog(this) != true)
        {
            return;
        }

        try
        {
            var result = await Task.Run(async () =>
            {
                using var scope = App.Services.CreateScope();
                var vault = scope.ServiceProvider.GetRequiredService<IPasswordVaultService>();
                return await vault.ImportChromeEdgeCsvAsync(profile.ProfileId, dlg.FileName);
            });

            var sb = new StringBuilder();
            sb.Append($"Impor CSV selesai → {profile.Name}: {result.ImportedCount} baru, {result.UpdatedCount} diperbarui, {result.SkippedCount} dilewati, {result.MalformedRowCount} baris tidak valid.");
            if (result.RedactedWarnings.Count > 0)
            {
                sb.Append(" " + string.Join(" ", result.RedactedWarnings));
            }

            VaultFeedbackText.Text = sb.ToString();
            await RefreshVaultCredentialsAsync();
        }
        catch (Exception ex)
        {
            VaultFeedbackText.Text = $"Impor CSV gagal: {ex.Message}";
        }
    }

    private async void OnSaveCredentialClick(object sender, RoutedEventArgs e)
    {
        if (VaultProfileCombo.SelectedItem is not BrowserProfile profile)
        {
            VaultFeedbackText.Text = "Pilih profil Switchly terlebih dahulu.";
            return;
        }

        var origin = VaultOriginBox.Text.Trim();
        var username = VaultUsernameBox.Text.Trim();
        var password = VaultPasswordBox.Password;

        try
        {
            using var scope = App.Services.CreateScope();
            var vault = scope.ServiceProvider.GetRequiredService<IPasswordVaultService>();
            await vault.UpsertCredentialAsync(profile.ProfileId, origin, username, password);
            VaultOriginBox.Clear();
            VaultUsernameBox.Clear();
            VaultPasswordBox.Clear();
            VaultFeedbackText.Text = "Kredensial berhasil disimpan terenkripsi ke Password Vault.";
            await RefreshVaultCredentialsAsync();
        }
        catch (Exception ex)
        {
            VaultFeedbackText.Text = ex.Message;
        }
    }

    private async void OnDeleteCredentialClick(object sender, RoutedEventArgs e)
    {
        if (VaultProfileCombo.SelectedItem is not BrowserProfile profile ||
            sender is not FrameworkElement { Tag: VaultCredentialSummary summary })
        {
            return;
        }

        using var scope = App.Services.CreateScope();
        var vault = scope.ServiceProvider.GetRequiredService<IPasswordVaultService>();
        await vault.DeleteAsync(profile.ProfileId, summary.CredentialId);
        VaultFeedbackText.Text = $"Kredensial untuk {summary.Origin} dihapus.";
        await RefreshVaultCredentialsAsync();
    }

    private async Task RunWithProgressAsync(Func<IProgress<BackupProgress>, CancellationToken, Task> action)
    {
        _opCts?.Dispose();
        _opCts = new CancellationTokenSource();
        CreateBackupButton.IsEnabled = false;
        StageRestoreButton.IsEnabled = false;
        CancelOpButton.Visibility = Visibility.Visible;
        OperationProgressBar.Visibility = Visibility.Visible;
        OperationProgressBar.Value = 0;

        var progress = new Progress<BackupProgress>(p =>
        {
            OperationProgressBar.Value = Math.Clamp(p.Fraction, 0, 1);
            BackupStatusText.Text = p.Stage;
        });

        try
        {
            await Task.Run(() => action(progress, _opCts.Token));
        }
        catch (OperationCanceledException)
        {
            BackupStatusText.Text = "Operasi dibatalkan. File sementara dibersihkan.";
        }
        catch (Exception ex)
        {
            BackupStatusText.Text = $"Gagal: {ex.Message}";
        }
        finally
        {
            CreateBackupButton.IsEnabled = true;
            StageRestoreButton.IsEnabled = true;
            CancelOpButton.Visibility = Visibility.Collapsed;
            OperationProgressBar.Visibility = Visibility.Collapsed;
            _opCts.Dispose();
            _opCts = null;
        }
    }
}
