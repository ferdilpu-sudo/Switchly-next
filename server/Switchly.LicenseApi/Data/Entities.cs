namespace Switchly.LicenseApi.Data;

public static class PlanCodes
{
    public const string ProMonthly = "pro_monthly";
    public const string ProAnnual = "pro_annual";
    public const string ProPerpetual = "pro_perpetual";
    public const string ProPerpetualEarlyBird = "pro_perpetual_early";

    public sealed record PlanDefinition(
        string Code,
        string DisplayName,
        long PriceIdr,
        int? DurationDays,
        int? UpdatesMonths,
        int MaxDevices);

    public static readonly IReadOnlyDictionary<string, PlanDefinition> Catalog =
        new Dictionary<string, PlanDefinition>(StringComparer.OrdinalIgnoreCase)
        {
            [ProMonthly] = new(ProMonthly, "Switchly Pro (Bulanan)", 39_000, DurationDays: 31, UpdatesMonths: null, MaxDevices: 2),
            [ProAnnual] = new(ProAnnual, "Switchly Pro (Tahunan)", 249_000, DurationDays: 366, UpdatesMonths: null, MaxDevices: 2),
            [ProPerpetual] = new(ProPerpetual, "Switchly Pro (Bayar Sekali + 1 Thn Update)", 399_000, DurationDays: null, UpdatesMonths: 12, MaxDevices: 2),
            [ProPerpetualEarlyBird] = new(ProPerpetualEarlyBird, "Switchly Pro (Early-Bird + 1 Thn Update)", 249_000, DurationDays: null, UpdatesMonths: 12, MaxDevices: 2),
        };
}

public sealed class Account
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EmailNormalized { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? TrialClaimedAtUtc { get; set; }
    public DateTimeOffset? TrialExpiresAtUtc { get; set; }
    public string Status { get; set; } = "active";

    public List<LicenseRecord> Licenses { get; set; } = [];
    public List<DeviceActivation> Devices { get; set; } = [];
}

public sealed class OtpChallenge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string EmailNormalized { get; set; } = string.Empty;
    public string CodeHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}

public sealed class SessionRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public string DeviceHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Account Account { get; set; } = null!;
}

public sealed class TrialDeviceClaim
{
    public string DeviceHash { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public DateTimeOffset ClaimedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LicenseRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTimeOffset StartsAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public DateTimeOffset? UpdatesUntilUtc { get; set; }
    public int MaxDevices { get; set; } = 2;
    public string PaymentProvider { get; set; } = "midtrans";
    public string ExternalOrderId { get; set; } = string.Empty;
    public long GrossAmountIdr { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Account Account { get; set; } = null!;
}

public sealed class PaymentOrder
{
    public string OrderId { get; set; } = string.Empty;
    public Guid AccountId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public long GrossAmountIdr { get; set; }
    public string Status { get; set; } = "pending";
    public string? SnapRedirectUrl { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAtUtc { get; set; }
}

public sealed class DeviceActivation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AccountId { get; set; }
    public string DeviceHash { get; set; } = string.Empty;
    public string DeviceLabel { get; set; } = string.Empty;
    public DateTimeOffset ActivatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAtUtc { get; set; }

    public Account Account { get; set; } = null!;
}
