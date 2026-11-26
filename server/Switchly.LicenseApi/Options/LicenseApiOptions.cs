namespace Switchly.LicenseApi.Options;

public sealed class LicenseApiOptions
{
    public const string SectionName = "LicenseApi";

    /// <summary>Base64-encoded 32-byte Ed25519 private key seed. Never commit to Git.</summary>
    public string Ed25519PrivateKeyBase64 { get; set; } = string.Empty;

    /// <summary>Random server pepper used when hashing OTP codes and session tokens.</summary>
    public string TokenHashPepper { get; set; } = string.Empty;

    public int FreeMaxProfiles { get; set; } = 5;
    public int TrialDays { get; set; } = 7;
    public int DefaultMaxDevices { get; set; } = 2;
    public int OfflineGraceDays { get; set; } = 14;
    public int TokenRefreshDays { get; set; } = 7;
    public int OtpExpiryMinutes { get; set; } = 10;
    public int OtpMaxAttempts { get; set; } = 5;
    public int SessionExpiryDays { get; set; } = 90;
}

public sealed class MidtransOptions
{
    public const string SectionName = "Midtrans";

    public bool IsProduction { get; set; }
    public string ServerKey { get; set; } = string.Empty;
    public string ClientKey { get; set; } = string.Empty;
    public string? FinishRedirectUrl { get; set; }

    public string SnapBaseUrl => IsProduction
        ? "https://app.midtrans.com"
        : "https://app.sandbox.midtrans.com";
}

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"Console" (dev) or "Resend" (production HTTPS API).</summary>
    public string Provider { get; set; } = "Console";
    public string ResendApiKey { get; set; } = string.Empty;
    public string FromAddress { get; set; } = "Switchly <noreply@switchly.id>";
}
