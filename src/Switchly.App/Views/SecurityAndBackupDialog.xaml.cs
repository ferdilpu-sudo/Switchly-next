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
            ? "Enkripsi Lokal Windows: Aktif & Sehat"
            : $"Enkripsi Lokal Windows: Bermasalah ({status.ErrorMessage})";
        HelloStatusText.Text = status.IsWindowsHelloAvailable
            ? "Windows Hello: Tersedia"
            : "Windows Hello: Belum tersedia pada instalasi standar (gunakan akun Windows + kata sandi utama opsional).";

        EnableMasterPasswordPanel.Visibility = status.IsMasterPasswordEnabled ? Visibility.Collapsed : Visibility.Visible;
        DisableMasterPasswordPanel.Visibility = status.IsMasterPasswordEnabled ? Visibility.Visible : Visibility.Collapsed;
    }

    private static void SetFeedback(TextBlock target, string message, bool isError = false, bool isNeutral = false)
    {
        target.Text = message;
        var resourceKey = isError
            ? "DangerSoftTextBrush"
            : isNeutral
                ? "TextSecondaryBrush"
                : "SuccessTextBrush";
        target.SetResourceReference(TextBlock.ForegroundProperty, resourceKey);
    }

    private void OnEnableMasterPasswordClick(object sender, RoutedEventArgs e)
    {
        var p1 = NewMasterPasswordBox.Password;
        var p2 = ConfirmMasterPasswordBox.Password;
        if (!string.Equals(p1, p2, StringComparison.Ordinal))
        {
            SetFeedback(SecurityFeedbackText, "Konfirmasi kata sandi utama tidak cocok.", isError: true);
            return;
        }

        try
        {
            _securityService.EnableMasterPassword(p1);
            NewMasterPasswordBox.Clear();
            ConfirmMasterPasswordBox.Clear();
            SetFeedback(SecurityFeedbackText, "Kunci kata sandi utama berhasil diaktifkan.");
            RefreshSecurityStatus();
        }
        catch (Exception ex)
        {
            SetFeedback(SecurityFeedbackText, ex.Message, isError: true);
        }
    }

    private void OnDisableMasterPasswordClick(object sender, RoutedEventArgs e)
    {
        var current = CurrentMasterPasswordBox.Password;
        if (_securityService.DisableMasterPassword(current))
        {
            CurrentMasterPasswordBox.Clear();
            SetFeedback(SecurityFeedbackText, "Kunci kata sandi utama dinonaktifkan.");
            RefreshSecurityStatus();
        }
        else
        {
            SetFeedback(SecurityFeedbackText, "Kata sandi utama saat ini salah.", isError: true);
            CurrentMasterPasswordBox.Clear();
        }
    }

    private async void OnCreateBackupClick(object sender, RoutedEventArgs e)
    {
        var pass = BackupPassphraseBox.Password;
        var confirm = BackupPassphraseConfirmBox.Password;
        if (string.IsNullOrEmpty(pass) || pass.Length < 8)
        {
            SetFeedback(BackupStatusText, "Passphrase minimal 8 karakter.", isError: true);
            return;
        }

        if (!string.Equals(pass, confirm, StringComparison.Ordinal))
        {
            SetFeedback(BackupStatusText, "Konfirmasi passphrase tidak cocok.", isError: true);
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

        // Close all active profile sessions cleanly before backup so profile storage & SQLite are flushed (F-18).
        await App.Services.GetRequiredService<Switchly.App.ViewModels.WorkspaceViewModel>().CloseAllSessionsAsync();

        await RunWithProgressAsync(async (progress, ct) =>
        {
            using var scope = App.Services.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupRestoreService>();
            var result = await backupService.CreateBackupAsync(dlg.FileName, pass, progress, ct);
            Dispatcher.Invoke(() =>
            {
                BackupPassphraseBox.Clear();
                BackupPassphraseConfirmBox.Clear();
                SetFeedback(
                    BackupStatusText,
                    $"Backup berhasil dibuat ({result.ProfileCount} profil, {result.ArchiveSizeBytes / 1024.0:F1} KB).");
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
            SetFeedback(BackupStatusText, string.Empty, isNeutral: true);
        }
        catch (Exception ex)
        {
            RestorePreviewCard.Visibility = Visibility.Collapsed;
            SetFeedback(BackupStatusText, $"Arsip tidak dapat dibaca: {ex.Message}", isError: true);
        }
    }

    private async void OnStageRestoreClick(object sender, RoutedEventArgs e)
    {
        var archivePath = RestoreArchivePathBox.Text;
        var pass = RestorePassphraseBox.Password;
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            SetFeedback(BackupStatusText, "Pilih file arsip .snbak terlebih dahulu.", isError: true);
            return;
        }

        if (string.IsNullOrEmpty(pass))
        {
            SetFeedback(BackupStatusText, "Masukkan passphrase arsip backup.", isError: true);
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
                SetFeedback(
                    BackupStatusText,
                    $"Staging selesai ({staged.ExtractedFileCount} file terverifikasi). Aplikasi perlu dimulai ulang untuk menyelesaikan pemulihan.");

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
                        Process.Start(new ProcessStartInfo(exePath, "--applying-restore") { UseShellExecute = true });
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
            SetFeedback(VaultFeedbackText, $"Gagal memuat Password Vault: {ex.Message}", isError: true);
        }
    }

    private async void OnImportCsvPasswordsClick(object sender, RoutedEventArgs e)
    {
        if (VaultProfileCombo.SelectedItem is not BrowserProfile profile)
        {
            SetFeedback(VaultFeedbackText, "Pilih profil Switchly terlebih dahulu.", isError: true);
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

            SetFeedback(VaultFeedbackText, sb.ToString());
            await RefreshVaultCredentialsAsync();
        }
        catch (Exception ex)
        {
            SetFeedback(VaultFeedbackText, $"Impor CSV gagal: {ex.Message}", isError: true);
        }
    }

    private async void OnSaveCredentialClick(object sender, RoutedEventArgs e)
    {
        if (VaultProfileCombo.SelectedItem is not BrowserProfile profile)
        {
            SetFeedback(VaultFeedbackText, "Pilih profil Switchly terlebih dahulu.", isError: true);
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
            SetFeedback(VaultFeedbackText, "Kredensial berhasil disimpan terenkripsi ke Password Vault.");
            await RefreshVaultCredentialsAsync();
        }
        catch (Exception ex)
        {
            SetFeedback(VaultFeedbackText, ex.Message, isError: true);
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
        SetFeedback(VaultFeedbackText, $"Kredensial untuk {summary.Origin} dihapus.", isNeutral: true);
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
            SetFeedback(BackupStatusText, p.Stage, isNeutral: true);
        });

        try
        {
            await Task.Run(() => action(progress, _opCts.Token));
        }
        catch (OperationCanceledException)
        {
            SetFeedback(BackupStatusText, "Operasi dibatalkan. File sementara dibersihkan.", isNeutral: true);
        }
        catch (Exception ex)
        {
            SetFeedback(BackupStatusText, $"Gagal: {ex.Message}", isError: true);
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
