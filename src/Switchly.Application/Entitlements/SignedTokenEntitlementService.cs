using Switchly.Domain.Entitlements;

namespace Switchly.Application.Entitlements;

/// <summary>
/// Offline-first GA entitlement engine (D16, REQ-012, REQ-013):
/// - Free tier (no account required): up to 5 active profiles, 0 network calls.
/// - Trial Pro (7 days, account required) & Pro Hybrid (Subscription / Perpetual):
///   verified locally via Ed25519 signed token cached with DPAPI + offline grace & clock-rollback guard.
/// - Non-destructive graceful downgrade: profiles above the Free quota are never deleted.
/// </summary>
public sealed class SignedTokenEntitlementService : IFeatureEntitlementService
{
    public const int DefaultFreeMaxProfiles = 5;
    private static readonly TimeSpan ClockRollbackTolerance = TimeSpan.FromMinutes(5);

    private readonly ILicenseStore _licenseStore;
    private readonly ILicenseTokenVerifier _tokenVerifier;
    private readonly IDeviceFingerprintProvider _deviceFingerprintProvider;
    private readonly TimeProvider _timeProvider;
    private readonly int _freeMaxProfiles;

    public SignedTokenEntitlementService(
        ILicenseStore licenseStore,
        ILicenseTokenVerifier tokenVerifier,
        IDeviceFingerprintProvider deviceFingerprintProvider,
        TimeProvider? timeProvider = null,
        int freeMaxProfiles = DefaultFreeMaxProfiles)
    {
        if (freeMaxProfiles <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(freeMaxProfiles));
        }

        _licenseStore = licenseStore;
        _tokenVerifier = tokenVerifier;
        _deviceFingerprintProvider = deviceFingerprintProvider;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _freeMaxProfiles = freeMaxProfiles;
    }

    public EntitlementSnapshot GetCurrentEntitlement()
    {
        var now = _timeProvider.GetUtcNow();
        var stored = _licenseStore.Load();
        if (stored is null || string.IsNullOrWhiteSpace(stored.SignedToken))
        {
            return CreateFreeSnapshot();
        }

        if (!_tokenVerifier.TryVerify(stored.SignedToken, out var payload) || payload is null)
        {
            return CreateFreeSnapshot();
        }

        var localDeviceHash = _deviceFingerprintProvider.GetDeviceHash();
        if (!string.Equals(payload.DeviceHash, localDeviceHash, StringComparison.Ordinal))
        {
            return CreateFreeSnapshot();
        }

        // Guard against manual system clock rollback to extend Trial/Subscription offline (SEC-06).
        if (now + ClockRollbackTolerance < stored.LastObservedUtc
            || now + ClockRollbackTolerance < payload.IssuedAtUtc)
        {
            return CreateFreeSnapshot();
        }

        // Ratchet LastObservedUtc forward periodically so offline clock rollbacks are detected.
        if (now - stored.LastObservedUtc >= TimeSpan.FromMinutes(15))
        {
            try
            {
                _licenseStore.Save(stored with { LastObservedUtc = now });
            }
            catch
            {
            }
        }

        var needsRefresh = now >= payload.RefreshAfterUtc;
        var tierKey = payload.Tier?.Trim().ToLowerInvariant();

        switch (tierKey)
        {
            case "pro_perpetual":
                if (now <= payload.OfflineGraceUntilUtc)
                {
                    return new EntitlementSnapshot(
                        ActiveTier: SubscriptionTier.ProPerpetual,
                        RequiresAccount: true,
                        MaxProfiles: payload.MaxProfiles,
                        IsOfflineReady: true,
                        DisplayBadge: "Pro (Perpetual)",
                        ExpiresAtUtc: null,
                        UpdatesUntilUtc: payload.UpdatesUntilUtc,
                        NeedsOnlineRefresh: needsRefresh);
                }

                return CreateFreeSnapshot(needsOnlineRefresh: true);

            case "pro_subscription":
                if (payload.ExpiresAtUtc is not null
                    && now <= payload.ExpiresAtUtc.Value
                    && now <= payload.OfflineGraceUntilUtc)
                {
                    return new EntitlementSnapshot(
                        ActiveTier: SubscriptionTier.ProSubscription,
                        RequiresAccount: true,
                        MaxProfiles: payload.MaxProfiles,
                        IsOfflineReady: true,
                        DisplayBadge: "Pro",
                        ExpiresAtUtc: payload.ExpiresAtUtc,
                        UpdatesUntilUtc: payload.UpdatesUntilUtc,
                        NeedsOnlineRefresh: needsRefresh);
                }

                return CreateFreeSnapshot(needsOnlineRefresh: true);

            case "trial_pro":
                if (payload.ExpiresAtUtc is not null && now <= payload.ExpiresAtUtc.Value)
                {
                    var remainingDays = Math.Max(1, (int)Math.Ceiling((payload.ExpiresAtUtc.Value - now).TotalDays));
                    return new EntitlementSnapshot(
                        ActiveTier: SubscriptionTier.TrialPro,
                        RequiresAccount: true,
                        MaxProfiles: payload.MaxProfiles,
                        IsOfflineReady: true,
                        DisplayBadge: $"Trial Pro (Sisa {remainingDays} Hari)",
                        ExpiresAtUtc: payload.ExpiresAtUtc,
                        UpdatesUntilUtc: payload.UpdatesUntilUtc,
                        NeedsOnlineRefresh: needsRefresh);
                }

                return CreateFreeSnapshot(needsOnlineRefresh: true);

            default:
                return CreateFreeSnapshot(needsOnlineRefresh: needsRefresh);
        }
    }

    public FeatureEntitlementDecision Evaluate(FeatureGateKey feature)
    {
        var snapshot = GetCurrentEntitlement();

        return feature switch
        {
            FeatureGateKey.ProfileManagement
                or FeatureGateKey.MultiWindowAndTabs
                or FeatureGateKey.BrowserImport
                or FeatureGateKey.PasswordVault
                or FeatureGateKey.ManualEncryptedBackup =>
                new FeatureEntitlementDecision(true, snapshot.ActiveTier),

            FeatureGateKey.ScheduledBackup =>
                new FeatureEntitlementDecision(
                    false,
                    snapshot.ActiveTier,
                    "Backup terjadwal otomatis belum diaktifkan pada versi ini (keputusan D2)."),

            FeatureGateKey.OptionalExtensions =>
                new FeatureEntitlementDecision(
                    false,
                    snapshot.ActiveTier,
                    "Dukungan ekstensi bersifat opsional dan ditunda (M-005)."),

            _ => new FeatureEntitlementDecision(
                false,
                snapshot.ActiveTier,
                "Fitur belum tersedia."),
        };
    }

    public FeatureEntitlementDecision CanCreateProfile(int currentActiveProfileCount)
    {
        if (currentActiveProfileCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentActiveProfileCount));
        }

        var snapshot = GetCurrentEntitlement();
        if (snapshot.MaxProfiles is null || currentActiveProfileCount < snapshot.MaxProfiles.Value)
        {
            return new FeatureEntitlementDecision(true, snapshot.ActiveTier);
        }

        return new FeatureEntitlementDecision(
            false,
            snapshot.ActiveTier,
            $"Paket Free mendukung hingga {snapshot.MaxProfiles.Value} profil aktif tanpa login. Buat akun untuk Trial Pro 7 hari gratis atau upgrade ke Pro.");
    }

    public FeatureEntitlementDecision CanOpenProfile(Guid profileId, IReadOnlyList<Guid> orderedActiveProfileIds)
    {
        ArgumentNullException.ThrowIfNull(orderedActiveProfileIds);

        var snapshot = GetCurrentEntitlement();
        if (snapshot.MaxProfiles is null)
        {
            return new FeatureEntitlementDecision(true, snapshot.ActiveTier);
        }

        var maxAllowed = snapshot.MaxProfiles.Value;
        var index = -1;
        for (var i = 0; i < orderedActiveProfileIds.Count; i++)
        {
            if (orderedActiveProfileIds[i] == profileId)
            {
                index = i;
                break;
            }
        }

        if (index < 0 || index < maxAllowed)
        {
            return new FeatureEntitlementDecision(true, snapshot.ActiveTier);
        }

        return new FeatureEntitlementDecision(
            false,
            snapshot.ActiveTier,
            $"Profil ini melebihi batas {maxAllowed} profil pada paket Free. Data profil tetap aman — upgrade ke Pro atau arsipkan profil lain untuk membukanya.");
    }

    private EntitlementSnapshot CreateFreeSnapshot(bool needsOnlineRefresh = false) =>
        new(
            ActiveTier: SubscriptionTier.Free,
            RequiresAccount: false,
            MaxProfiles: _freeMaxProfiles,
            IsOfflineReady: true,
            DisplayBadge: $"Free (Maks. {_freeMaxProfiles} Profil)",
            NeedsOnlineRefresh: needsOnlineRefresh);
}
