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

    public async Task<bool> IsTrialEligibleAsync(Account account, string? deviceHash, CancellationToken ct)
    {
        if (account.TrialClaimedAtUtc is not null)
        {
            return false;
        }

        var normalizedDevice = deviceHash?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDevice))
        {
            return false;
        }

        var deviceClaimed = await db.TrialDeviceClaims.AnyAsync(x => x.DeviceHash == normalizedDevice, ct);
        return !deviceClaimed;
    }

    public async Task<TrialStartResult> StartTrialAsync(
        Account account,
        string? deviceHash,
        string? deviceLabel,
        CancellationToken ct)
    {
        var normalizedDevice = deviceHash?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDevice) || normalizedDevice.Length > 128)
        {
            return new TrialStartResult(false, ErrorMessage: "Identitas perangkat tidak valid.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // Reload fresh account state inside transaction to close TOCTOU window (S1-03).
        var freshAccount = await db.Accounts.FirstAsync(a => a.Id == account.Id, ct);
        if (freshAccount.TrialClaimedAtUtc is not null)
        {
            return new TrialStartResult(false, ErrorMessage: "Akun ini sudah pernah menggunakan Trial Pro 7 hari.");
        }

        var deviceAlreadyClaimed = await db.TrialDeviceClaims.AnyAsync(x => x.DeviceHash == normalizedDevice, ct);
        if (deviceAlreadyClaimed)
        {
            return new TrialStartResult(false, ErrorMessage: "Perangkat ini sudah pernah menggunakan Trial Pro 7 hari.");
        }

        // Prevent Gmail dot/plus-alias abuse from claiming multiple 7-day trials with the same inbox (#5).
        var canonicalEmail = OtpService.CanonicalizeForTrialCheck(freshAccount.EmailNormalized);
        var claimedEmails = await db.Accounts
            .Where(a => a.TrialClaimedAtUtc != null)
            .Select(a => a.EmailNormalized)
            .ToListAsync(ct);

        if (claimedEmails.Any(e => string.Equals(OtpService.CanonicalizeForTrialCheck(e), canonicalEmail, StringComparison.OrdinalIgnoreCase)))
        {
            return new TrialStartResult(false, ErrorMessage: "Alamat email (atau alias) ini sudah pernah menggunakan Trial Pro 7 hari.");
        }

        // Verify device slot BEFORE marking trial claimed so MaxDevices failure never burns the user's one-time trial (S1-03).
        var allDevices = await db.DeviceActivations
            .Where(d => d.AccountId == freshAccount.Id)
            .ToListAsync(ct);
        var activeDevicesCount = allDevices.Count(d => d.RevokedAtUtc == null && d.DeviceHash != normalizedDevice);
        if (activeDevicesCount >= _options.DefaultMaxDevices)
        {
            return new TrialStartResult(
                false,
                ErrorMessage: $"Batas perangkat aktif ({_options.DefaultMaxDevices} PC) tercapai. Nonaktifkan perangkat lama terlebih dahulu sebelum mengaktifkan Trial.");
        }

        var now = timeProvider.GetUtcNow();
        freshAccount.TrialClaimedAtUtc = now;
        freshAccount.TrialExpiresAtUtc = now.AddDays(_options.TrialDays);

        db.TrialDeviceClaims.Add(new TrialDeviceClaim
        {
            DeviceHash = normalizedDevice,
            AccountId = freshAccount.Id,
            ClaimedAtUtc = now,
        });

        try
        {
            await db.SaveChangesAsync(ct);

            var issued = await IssueForDeviceCoreAsync(freshAccount, normalizedDevice, deviceLabel, ct);
            if (!issued.Succeeded)
            {
                await tx.RollbackAsync(ct);
                return new TrialStartResult(false, ErrorMessage: issued.ErrorMessage);
            }

            await tx.CommitAsync(ct);
            return new TrialStartResult(true, issued.EntitlementToken, freshAccount.TrialExpiresAtUtc);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return new TrialStartResult(false, ErrorMessage: "Perangkat atau akun ini sudah pernah mengklaim Trial Pro 7 hari.");
        }
    }

    public async Task<EntitlementIssueResult> IssueForDeviceAsync(
        Account account,
        string? deviceHash,
        string? deviceLabel,
        CancellationToken ct)
    {
        var normalizedDevice = deviceHash?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedDevice) || normalizedDevice.Length > 128)
        {
            return new EntitlementIssueResult(false, ErrorMessage: "Identitas perangkat tidak valid.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        try
        {
            var result = await IssueForDeviceCoreAsync(account, normalizedDevice, deviceLabel, ct);
            if (result.Succeeded)
            {
                await tx.CommitAsync(ct);
            }
            else
            {
                await tx.RollbackAsync(ct);
            }

            return result;
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return new EntitlementIssueResult(false, ErrorMessage: "Aktivasi perangkat sedang diproses secara bersamaan. Silakan ulangi.");
        }
    }

    private async Task<EntitlementIssueResult> IssueForDeviceCoreAsync(
        Account account,
        string normalizedDevice,
        string? deviceLabel,
        CancellationToken ct)
    {
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

        // Query ALL device rows (active + revoked) so re-activating a previously revoked PC reuses the unique (AccountId, DeviceHash) row (S1-05).
        var allDevices = await db.DeviceActivations
            .Where(d => d.AccountId == account.Id)
            .ToListAsync(ct);

        var activeOtherCount = allDevices.Count(
            d => d.RevokedAtUtc == null && !string.Equals(d.DeviceHash, normalizedDevice, StringComparison.Ordinal));

        var existingDevice = allDevices.FirstOrDefault(
            d => string.Equals(d.DeviceHash, normalizedDevice, StringComparison.Ordinal));

        var cleanLabel = string.IsNullOrWhiteSpace(deviceLabel)
            ? "Windows PC"
            : deviceLabel.Trim()[..Math.Min(128, deviceLabel.Trim().Length)];

        if (existingDevice is not null && existingDevice.RevokedAtUtc is null)
        {
            existingDevice.LastSeenUtc = now;
            if (!string.IsNullOrWhiteSpace(deviceLabel))
            {
                existingDevice.DeviceLabel = cleanLabel;
            }
        }
        else
        {
            if (activeOtherCount >= maxDevices)
            {
                return new EntitlementIssueResult(
                    false,
                    ErrorMessage: $"Batas perangkat aktif ({maxDevices} PC) tercapai. Nonaktifkan perangkat lama terlebih dahulu di Pengaturan → Akun & Lisensi.");
            }

            if (existingDevice is not null)
            {
                existingDevice.RevokedAtUtc = null;
                existingDevice.ActivatedAtUtc = now;
                existingDevice.LastSeenUtc = now;
                existingDevice.DeviceLabel = cleanLabel;
            }
            else
            {
                db.DeviceActivations.Add(new DeviceActivation
                {
                    AccountId = account.Id,
                    DeviceHash = normalizedDevice,
                    DeviceLabel = cleanLabel,
                    ActivatedAtUtc = now,
                    LastSeenUtc = now,
                });
            }
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
            // 60-day rolling offline grace (auto-extended on every online refresh) so chargeback revocations propagate (#4b).
            offlineGraceUntil = now.AddDays(60);
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
