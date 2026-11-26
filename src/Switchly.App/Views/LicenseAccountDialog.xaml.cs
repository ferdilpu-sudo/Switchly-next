using System.Diagnostics;
using System.Windows;
using Switchly.Application.Entitlements;
using Switchly.Domain.Entitlements;

namespace Switchly.App.Views;

public partial class LicenseAccountDialog : Window
{
    private readonly LicenseAccountService _accountService;
    private readonly string? _initialReason;

    public LicenseAccountDialog(LicenseAccountService accountService, string? initialReason = null)
    {
        InitializeComponent();
        _accountService = accountService;
        _initialReason = initialReason;
        RefreshUiState();
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

        SendOtpButton.IsEnabled = _accountService.IsBackendConfigured;
        VerifyOtpButton.IsEnabled = _accountService.IsBackendConfigured;

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
                ? "Kamu sedang menggunakan Switchly Beta Gratis tanpa perlu akun."
                : "Mode Free (maks. 5 profil) berjalan 100% lokal tanpa login.";
        }
    }

    private async void OnSendOtpClick(object sender, RoutedEventArgs e)
    {
        var email = EmailTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(email))
        {
            StatusText.Text = "Masukkan alamat email terlebih dahulu.";
            return;
        }

        SendOtpButton.IsEnabled = false;
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
        finally
        {
            SendOtpButton.IsEnabled = _accountService.IsBackendConfigured;
        }
    }

    private async void OnVerifyOtpClick(object sender, RoutedEventArgs e)
    {
        var email = EmailTextBox.Text.Trim();
        var code = OtpCodeTextBox.Text.Trim();
        if (code.Length != 6)
        {
            StatusText.Text = "Masukkan kode OTP 6 digit.";
            return;
        }

        VerifyOtpButton.IsEnabled = false;
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
        finally
        {
            VerifyOtpButton.IsEnabled = _accountService.IsBackendConfigured;
        }
    }

    private async void OnStartTrialClick(object sender, RoutedEventArgs e)
    {
        StartTrialButton.IsEnabled = false;
        StatusText.Text = "Mengaktifkan Trial Pro 7 hari...";
        try
        {
            var result = await _accountService.StartTrialAsync();
            StatusText.Text = result.Succeeded
                ? "Trial Pro 7 hari aktif! Semua fitur Pro sudah terbuka."
                : result.ErrorMessage ?? "Gagal mengaktifkan Trial Pro.";
            RefreshUiState();
        }
        finally
        {
            StartTrialButton.IsEnabled = true;
        }
    }

    private void OnCheckoutMonthlyClick(object sender, RoutedEventArgs e) => _ = StartCheckoutAsync("pro_monthly");

    private void OnCheckoutAnnualClick(object sender, RoutedEventArgs e) => _ = StartCheckoutAsync("pro_annual");

    private void OnCheckoutPerpetualClick(object sender, RoutedEventArgs e) => _ = StartCheckoutAsync("pro_perpetual");

    private async Task StartCheckoutAsync(string planCode)
    {
        StatusText.Text = "Membuat halaman pembayaran Midtrans (QRIS/VA)...";
        var result = await _accountService.StartCheckoutAsync(planCode);
        if (!result.Succeeded || string.IsNullOrWhiteSpace(result.CheckoutUrl))
        {
            StatusText.Text = result.ErrorMessage ?? "Gagal membuka halaman pembayaran.";
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = result.CheckoutUrl,
                UseShellExecute = true,
            });
            StatusText.Text = $"Halaman pembayaran Midtrans dibuka (Order {result.OrderId}). Setelah selesai bayar, klik 'Perbarui Status Lisensi'.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Tidak dapat membuka browser: {ex.Message}";
        }
    }

    private async void OnRefreshLicenseClick(object sender, RoutedEventArgs e)
    {
        RefreshLicenseButton.IsEnabled = false;
        StatusText.Text = "Memeriksa status lisensi terbaru...";
        try
        {
            var result = await _accountService.RefreshAsync();
            StatusText.Text = result.Succeeded
                ? "Status lisensi berhasil diperbarui."
                : result.ErrorMessage ?? "Gagal memperbarui status lisensi.";
            RefreshUiState();
        }
        finally
        {
            RefreshLicenseButton.IsEnabled = true;
        }
    }

    private void OnSignOutClick(object sender, RoutedEventArgs e)
    {
        _accountService.SignOut();
        OtpVerifyRow.Visibility = Visibility.Collapsed;
        OtpCodeTextBox.Clear();
        StatusText.Text = "Berhasil keluar. Semua profil lokal kamu tetap aman.";
        RefreshUiState();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
