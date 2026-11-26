using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Switchly.LicenseApi.Crypto;
using Switchly.LicenseApi.Data;
using Switchly.LicenseApi.Options;

namespace Switchly.LicenseApi.Services;

public sealed record TrialStartResult(
    bool Succeeded,
    string? EntitlementToken = null,
    DateTimeOffset? TrialExpiresAtUtc = null,
    string? ErrorMessage = null);

public sealed record EntitlementIssueResult(
    bool Succeeded,
    string? EntitlementToken = null,
    LicenseTokenPayload? Payload = null,
    bool TrialEligible = false,
    string? ErrorMessage = null);

public sealed class EntitlementIssuer(
    LicenseDbContext db,
    IEd25519TokenService tokenService,
    IOptions<LicenseApiOptions> options,
    TimeProvider timeProvider)
{
    private readonly LicenseApiOptions _options = options.Value;

    public async Task<bool> IsTrialEligibleAsync(Account account, string deviceHash, CancellationToken ct)
    {
        if (account.TrialClaimedAtUtc is not null)
        {
            return false;
        }

        var normalizedDevice = deviceHash.Trim();
        var deviceClaimed = await db.TrialDeviceClaims.AnyAsync(x => x.DeviceHash == normalizedDevice, ct);
        return !deviceClaimed;
    }

    public async Task<TrialStartResult> StartTrialAsync(
        Account account,
        string deviceHash,
        string deviceLabel,
        CancellationToken ct)
    {
        var normalizedDevice = deviceHash.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDevice))
        {
            return new TrialStartResult(false, ErrorMessage: "Identitas perangkat tidak valid.");
        }

        if (account.TrialClaimedAtUtc is not null)
        {
            return new TrialStartResult(false, ErrorMessage: "Akun ini sudah pernah menggunakan Trial Pro 7 hari.");
        }

        var deviceAlreadyClaimed = await db.TrialDeviceClaims.AnyAsync(x => x.DeviceHash == normalizedDevice, ct);
        if (deviceAlreadyClaimed)
        {
            return new TrialStartResult(false, ErrorMessage: "Perangkat ini sudah pernah menggunakan Trial Pro 7 hari.");
        }

        var now = timeProvider.GetUtcNow();
        account.TrialClaimedAtUtc = now;
        account.TrialExpiresAtUtc = now.AddDays(_options.TrialDays);

        db.TrialDeviceClaims.Add(new TrialDeviceClaim
        {
            DeviceHash = normalizedDevice,
            AccountId = account.Id,
            ClaimedAtUtc = now,
        });

        await db.SaveChangesAsync(ct);

        var issued = await IssueForDeviceAsync(account, normalizedDevice, deviceLabel, ct);
        if (!issued.Succeeded)
        {
            return new TrialStartResult(false, ErrorMessage: issued.ErrorMessage);
        }

        return new TrialStartResult(true, issued.EntitlementToken, account.TrialExpiresAtUtc);
    }

    public async Task<EntitlementIssueResult> IssueForDeviceAsync(
        Account account,
        string deviceHash,
        string deviceLabel,
        CancellationToken ct)
    {
        var normalizedDevice = deviceHash.Trim();
        var now = timeProvider.GetUtcNow();

        var licenses = await db.Licenses
            .Where(l => l.AccountId == account.Id && l.Status == "active")
            .ToListAsync(ct);

        var activePerpetual = licenses
            .Where(l => l.ExpiresAtUtc is null)
            .OrderByDescending(l => l.UpdatesUntilUtc)
            .FirstOrDefault();

        var activeSubscription = licenses
            .Where(l => l.ExpiresAtUtc is not null && l.ExpiresAtUtc > now)
            .OrderByDescending(l => l.ExpiresAtUtc)
            .FirstOrDefault();

        var isTrialActive = account.TrialExpiresAtUtc is not null && account.TrialExpiresAtUtc > now;

        var maxDevices = activePerpetual?.MaxDevices
            ?? activeSubscription?.MaxDevices
            ?? _options.DefaultMaxDevices;

        var activeDevices = await db.DeviceActivations
            .Where(d => d.AccountId == account.Id && d.RevokedAtUtc == null)
            .ToListAsync(ct);

        var existingDevice = activeDevices.FirstOrDefault(
            d => string.Equals(d.DeviceHash, normalizedDevice, StringComparison.Ordinal));

        if (existingDevice is not null)
        {
            existingDevice.LastSeenUtc = now;
            if (!string.IsNullOrWhiteSpace(deviceLabel))
            {
                existingDevice.DeviceLabel = deviceLabel.Trim()[..Math.Min(128, deviceLabel.Trim().Length)];
            }
        }
        else
        {
            if (activeDevices.Count >= maxDevices)
            {
                return new EntitlementIssueResult(
                    false,
                    ErrorMessage: $"Batas perangkat aktif ({maxDevices} PC) tercapai. Nonaktifkan perangkat lama terlebih dahulu di Pengaturan → Akun & Lisensi.");
            }

            var cleanLabel = string.IsNullOrWhiteSpace(deviceLabel)
                ? "Windows PC"
                : deviceLabel.Trim()[..Math.Min(128, deviceLabel.Trim().Length)];

            db.DeviceActivations.Add(new DeviceActivation
            {
                AccountId = account.Id,
                DeviceHash = normalizedDevice,
                DeviceLabel = cleanLabel,
                ActivatedAtUtc = now,
                LastSeenUtc = now,
            });
        }

        await db.SaveChangesAsync(ct);

        string tier;
        int? maxProfiles;
        DateTimeOffset? expiresAt;
        DateTimeOffset? updatesUntil;
        DateTimeOffset offlineGraceUntil;

        if (activePerpetual is not null)
        {
            tier = "pro_perpetual";
            maxProfiles = null;
            expiresAt = null;
            updatesUntil = activePerpetual.UpdatesUntilUtc;
            offlineGraceUntil = now.AddYears(100);
        }
        else if (activeSubscription is not null)
        {
            tier = "pro_subscription";
            maxProfiles = null;
            expiresAt = activeSubscription.ExpiresAtUtc;
            updatesUntil = activeSubscription.ExpiresAtUtc;
            offlineGraceUntil = now.AddDays(_options.OfflineGraceDays);
        }
        else if (isTrialActive)
        {
            tier = "trial_pro";
            maxProfiles = null;
            expiresAt = account.TrialExpiresAtUtc;
            updatesUntil = account.TrialExpiresAtUtc;
            offlineGraceUntil = account.TrialExpiresAtUtc!.Value;
        }
        else
        {
            tier = "free";
            maxProfiles = _options.FreeMaxProfiles;
            expiresAt = null;
            updatesUntil = null;
            offlineGraceUntil = now.AddDays(365);
        }

        var payload = new LicenseTokenPayload(
            Version: 1,
            AccountId: account.Id.ToString("N"),
            Tier: tier,
            MaxProfiles: maxProfiles,
            DeviceHash: normalizedDevice,
            IssuedAtUtc: now,
            ExpiresAtUtc: expiresAt,
            UpdatesUntilUtc: updatesUntil,
            RefreshAfterUtc: now.AddDays(_options.TokenRefreshDays),
            OfflineGraceUntilUtc: offlineGraceUntil);

        var signedToken = tokenService.Sign(payload);
        var trialEligible = await IsTrialEligibleAsync(account, normalizedDevice, ct);
        return new EntitlementIssueResult(true, signedToken, payload, trialEligible);
    }
}
