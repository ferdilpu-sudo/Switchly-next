using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using Switchly.Application.Entitlements;
using Switchly.Domain.Entitlements;

namespace Switchly.App.Views;

public partial class LicenseAccountDialog : Window
{
    private readonly LicenseAccountService _accountService;
    private readonly string? _initialReason;
    private bool _isBusy;

    public LicenseAccountDialog(LicenseAccountService accountService, string? initialReason = null)
    {
        InitializeComponent();
        _accountService = accountService;
        _initialReason = initialReason;
        RefreshUiState();
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        var canCallApi = !busy && _accountService.IsBackendConfigured;
        SendOtpButton.IsEnabled = canCallApi;
        VerifyOtpButton.IsEnabled = canCallApi;
        StartTrialButton.IsEnabled = !busy;
        CheckoutMonthlyButton.IsEnabled = !busy;
        CheckoutAnnualButton.IsEnabled = !busy;
        CheckoutPerpetualButton.IsEnabled = !busy;
        RefreshLicenseButton.IsEnabled = !busy;
        SignOutButton.IsEnabled = !busy;
    }

    private void RefreshUiState()
    {
        var snapshot = _accountService.GetSnapshot();
        var stored = _accountService.GetStoredState();

        PlanBadgeText.Text = snapshot.DisplayBadge;

        if (!string.IsNullOrWhiteSpace(_initialReason))
        {
            InfoBanner.Visibility = Visibility.Visible;
            InfoBannerText.Text = _initialReason;
        }
        else if (!_accountService.IsBackendConfigured)
        {
            InfoBanner.Visibility = Visibility.Visible;
            InfoBannerText.Text = "Mode Beta Gratis aktif (tanpa batas profil dan tanpa perlu akun). Setelah masa Beta selesai, paket Free mendukung 5 profil tanpa login + Trial Pro 7 hari gratis.";
        }
        else
        {
            InfoBanner.Visibility = Visibility.Collapsed;
        }

        var isSignedIn = stored is not null && !string.IsNullOrWhiteSpace(stored.SessionToken);
        LoginSection.Visibility = isSignedIn ? Visibility.Collapsed : Visibility.Visible;
        SignedInSection.Visibility = isSignedIn ? Visibility.Visible : Visibility.Collapsed;

        SetBusy(_isBusy);

        if (isSignedIn && stored is not null)
        {
            SignedInEmailText.Text = stored.AccountEmail ?? "Akun Switchly";
            AccountSubtitleText.Text = "Lisensi terverifikasi secara lokal dan tetap aktif saat perangkat offline.";

            LicenseExpiryText.Text = snapshot.ActiveTier switch
            {
                SubscriptionTier.ProPerpetual => snapshot.UpdatesUntilUtc is { } upd
                    ? $"Pro Perpetual • Gratis update hingga {upd.ToLocalTime():dd MMM yyyy}"
                    : "Pro Perpetual • Aktif selamanya",
                SubscriptionTier.ProSubscription when snapshot.ExpiresAtUtc is { } exp =>
                    $"Pro Langganan • Aktif hingga {exp.ToLocalTime():dd MMM yyyy}",
                SubscriptionTier.TrialPro when snapshot.ExpiresAtUtc is { } exp =>
                    $"Trial Pro 7 Hari • Berakhir {exp.ToLocalTime():dd MMM yyyy HH:mm}",
                _ => $"Paket {snapshot.DisplayBadge}",
            };

            StartTrialButton.Visibility = snapshot.ActiveTier is SubscriptionTier.Free or SubscriptionTier.FreeBeta
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
        else
        {
            AccountSubtitleText.Text = snapshot.ActiveTier == SubscriptionTier.FreeBeta
                ? "Anda sedang menggunakan Switchly Beta Gratis tanpa perlu akun."
                : "Mode Free (maks. 5 profil) berjalan 100% lokal tanpa login.";
        }
    }

    private void OnEmailBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && SendOtpButton.IsEnabled)
        {
            OnSendOtpClick(sender, e);
            e.Handled = true;
        }
    }

    private void OnOtpCodeBoxKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && VerifyOtpButton.IsEnabled)
        {
            OnVerifyOtpClick(sender, e);
            e.Handled = true;
        }
    }

    private async void OnSendOtpClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var email = EmailTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            StatusText.Text = "Masukkan alamat email terlebih dahulu.";
            return;
        }

        SetBusy(true);
        StatusText.Text = "Mengirim kode OTP ke email...";
        try
        {
            var result = await _accountService.RequestOtpAsync(email);
            StatusText.Text = result.Message ?? (result.Succeeded ? "Kode OTP terkirim." : "Gagal mengirim OTP.");
            if (result.Succeeded)
            {
                OtpVerifyRow.Visibility = Visibility.Visible;
                OtpCodeTextBox.Focus();
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Terjadi kesalahan: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnVerifyOtpClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        var email = EmailTextBox.Text.Trim();
        var code = OtpCodeTextBox.Text.Trim();
        if (code.Length != 6)
        {
            StatusText.Text = "Masukkan kode OTP 6 digit.";
            return;
        }

        SetBusy(true);
        StatusText.Text = "Memverifikasi OTP...";
        try
        {
            var result = await _accountService.VerifyOtpAsync(email, code);
            if (!result.Succeeded)
            {
                StatusText.Text = result.ErrorMessage ?? "Verifikasi OTP gagal.";
                return;
            }

            StatusText.Text = "Berhasil masuk! Status lisensi diperbarui.";
            RefreshUiState();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Terjadi kesalahan: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnStartTrialClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        StatusText.Text = "Mengaktifkan Trial Pro 7 hari...";
        try
        {
            var result = await _accountService.StartTrialAsync();
            StatusText.Text = result.Succeeded
                ? "Trial Pro 7 hari aktif! Semua fitur Pro sudah terbuka."
                : result.ErrorMessage ?? "Gagal mengaktifkan Trial Pro.";
            RefreshUiState();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Terjadi kesalahan: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnCheckoutMonthlyClick(object sender, RoutedEventArgs e) => await StartCheckoutAsync("pro_monthly");

    private async void OnCheckoutAnnualClick(object sender, RoutedEventArgs e) => await StartCheckoutAsync("pro_annual");

    private async void OnCheckoutPerpetualClick(object sender, RoutedEventArgs e) => await StartCheckoutAsync("pro_perpetual");

    private async Task StartCheckoutAsync(string planCode)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        StatusText.Text = "Membuat halaman pembayaran Midtrans (QRIS/VA)...";
        try
        {
            var result = await _accountService.StartCheckoutAsync(planCode);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.CheckoutUrl))
            {
                StatusText.Text = result.ErrorMessage ?? "Gagal membuka halaman pembayaran.";
                return;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = result.CheckoutUrl,
                UseShellExecute = true,
            });
            StatusText.Text = $"Halaman pembayaran Midtrans dibuka (Order {result.OrderId}). Setelah selesai bayar, klik 'Perbarui Status Lisensi'.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Tidak dapat membuka halaman pembayaran: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnRefreshLicenseClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        StatusText.Text = "Memeriksa status lisensi terbaru...";
        try
        {
            var result = await _accountService.RefreshAsync();
            StatusText.Text = result.Succeeded
                ? "Status lisensi berhasil diperbarui."
                : result.ErrorMessage ?? "Gagal memperbarui status lisensi.";
            RefreshUiState();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Terjadi kesalahan: {ex.Message}";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
        {
            return;
        }

        SetBusy(true);
        try
        {
            await _accountService.SignOutAsync();
            OtpVerifyRow.Visibility = Visibility.Collapsed;
            OtpCodeTextBox.Clear();
            StatusText.Text = "Berhasil keluar. Semua profil lokal Anda tetap aman.";
            RefreshUiState();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
