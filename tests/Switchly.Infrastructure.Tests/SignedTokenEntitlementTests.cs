using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Switchly.Application.Entitlements;
using Switchly.Domain.Entitlements;
using Switchly.Infrastructure.Entitlements;
using Switchly.Infrastructure.Security;

namespace Switchly.Infrastructure.Tests;

public sealed class SignedTokenEntitlementTests : IDisposable
{
    private readonly string _tempDir;
    private readonly Ed25519PrivateKeyParameters _privateKey;
    private readonly Ed25519LicenseTokenVerifier _verifier;

    public SignedTokenEntitlementTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"switchly-ent-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        var seed = RandomNumberGenerator.GetBytes(32);
        _privateKey = new Ed25519PrivateKeyParameters(seed, 0);
        var pubBase64 = Convert.ToBase64String(_privateKey.GeneratePublicKey().GetEncoded());
        _verifier = new Ed25519LicenseTokenVerifier(pubBase64);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Best-effort temp cleanup.
        }
    }

    [Fact]
    public void FreeTier_NoToken_AllowsFiveProfilesAndGracefullyLocksSixth()
    {
        var store = new InMemoryLicenseStore();
        var fp = new FixedFingerprintProvider("sha256:device-1");
        var clock = new FixedTimeProvider(new DateTimeOffset(2026, 11, 26, 6, 0, 0, TimeSpan.Zero));
        var svc = new SignedTokenEntitlementService(store, _verifier, fp, clock);

        var snapshot = svc.GetCurrentEntitlement();
        Assert.Equal(SubscriptionTier.Free, snapshot.ActiveTier);
        Assert.False(snapshot.RequiresAccount);
        Assert.Equal(5, snapshot.MaxProfiles);
        Assert.True(snapshot.IsOfflineReady);

        Assert.True(svc.CanCreateProfile(4).IsAllowed);
        Assert.False(svc.CanCreateProfile(5).IsAllowed);

        var profiles = Enumerable.Range(0, 6).Select(_ => Guid.NewGuid()).ToList();
        Assert.True(svc.CanOpenProfile(profiles[0], profiles).IsAllowed);
        Assert.True(svc.CanOpenProfile(profiles[4], profiles).IsAllowed);
        Assert.False(svc.CanOpenProfile(profiles[5], profiles).IsAllowed);
    }

    [Fact]
    public void TrialPro_ValidToken_AllowsUnlimitedProfiles_AndDowngradesAfterExpiry()
    {
        var now = new DateTimeOffset(2026, 11, 26, 6, 0, 0, TimeSpan.Zero);
        var payload = new SignedLicensePayload(
            Version: 1,
            AccountId: "acct-trial",
            Tier: "trial_pro",
            MaxProfiles: null,
            DeviceHash: "sha256:device-1",
            IssuedAtUtc: now,
            ExpiresAtUtc: now.AddDays(7),
            UpdatesUntilUtc: now.AddDays(7),
            RefreshAfterUtc: now.AddDays(7),
            OfflineGraceUntilUtc: now.AddDays(7));

        var store = new InMemoryLicenseStore();
        store.Save(new StoredLicenseState(Sign(payload), "sess", "user@example.com", now));

        var clock = new FixedTimeProvider(now.AddDays(2));
        var svc = new SignedTokenEntitlementService(store, _verifier, new FixedFingerprintProvider("sha256:device-1"), clock);

        var active = svc.GetCurrentEntitlement();
        Assert.Equal(SubscriptionTier.TrialPro, active.ActiveTier);
        Assert.Null(active.MaxProfiles);
        Assert.Contains("5 Hari", active.DisplayBadge, StringComparison.Ordinal);
        Assert.True(svc.CanCreateProfile(50).IsAllowed);

        // Day 8 -> trial expired -> graceful downgrade to Free (5 profiles)
        clock.CurrentUtc = now.AddDays(7).AddMinutes(1);
        var expired = svc.GetCurrentEntitlement();
        Assert.Equal(SubscriptionTier.Free, expired.ActiveTier);
        Assert.Equal(5, expired.MaxProfiles);
        Assert.True(expired.NeedsOnlineRefresh);
    }

    [Fact]
    public void ProSubscription_RespectsOfflineGraceAndClockRollbackGuard()
    {
        var now = new DateTimeOffset(2026, 11, 26, 6, 0, 0, TimeSpan.Zero);
        var payload = new SignedLicensePayload(
            Version: 1,
            AccountId: "acct-pro",
            Tier: "pro_subscription",
            MaxProfiles: null,
            DeviceHash: "sha256:device-1",
            IssuedAtUtc: now,
            ExpiresAtUtc: now.AddDays(30),
            UpdatesUntilUtc: now.AddDays(30),
            RefreshAfterUtc: now.AddDays(7),
            OfflineGraceUntilUtc: now.AddDays(14));

        var store = new InMemoryLicenseStore();
        store.Save(new StoredLicenseState(Sign(payload), "sess", "pro@example.com", now.AddDays(3)));

        var clock = new FixedTimeProvider(now.AddDays(10));
        var svc = new SignedTokenEntitlementService(store, _verifier, new FixedFingerprintProvider("sha256:device-1"), clock);

        var withinGrace = svc.GetCurrentEntitlement();
        Assert.Equal(SubscriptionTier.ProSubscription, withinGrace.ActiveTier);
        Assert.True(withinGrace.NeedsOnlineRefresh);

        // Beyond 14-day offline grace -> Free until refreshed online
        clock.CurrentUtc = now.AddDays(15);
        Assert.Equal(SubscriptionTier.Free, svc.GetCurrentEntitlement().ActiveTier);

        // Clock rolled back before LastObservedUtc -> fails closed to Free
        clock.CurrentUtc = now.AddDays(1);
        Assert.Equal(SubscriptionTier.Free, svc.GetCurrentEntitlement().ActiveTier);
    }

    [Fact]
    public void TamperedTokenOrWrongDevice_FailsClosedToFree()
    {
        var now = new DateTimeOffset(2026, 11, 26, 6, 0, 0, TimeSpan.Zero);
        var payload = new SignedLicensePayload(
            Version: 1,
            AccountId: "acct-perp",
            Tier: "pro_perpetual",
            MaxProfiles: null,
            DeviceHash: "sha256:device-1",
            IssuedAtUtc: now,
            ExpiresAtUtc: null,
            UpdatesUntilUtc: now.AddYears(1),
            RefreshAfterUtc: now.AddDays(7),
            OfflineGraceUntilUtc: now.AddYears(100));

        var validToken = Sign(payload);
        var store = new InMemoryLicenseStore();
        var clock = new FixedTimeProvider(now);

        // Wrong device -> Free
        store.Save(new StoredLicenseState(validToken, "sess", "a@b.com", now));
        var wrongDeviceSvc = new SignedTokenEntitlementService(store, _verifier, new FixedFingerprintProvider("sha256:other-pc"), clock);
        Assert.Equal(SubscriptionTier.Free, wrongDeviceSvc.GetCurrentEntitlement().ActiveTier);

        // Tampered token -> Free
        var tampered = (validToken[0] == 'A' ? 'B' : 'A') + validToken[1..];
        store.Save(new StoredLicenseState(tampered, "sess", "a@b.com", now));
        var tamperedSvc = new SignedTokenEntitlementService(store, _verifier, new FixedFingerprintProvider("sha256:device-1"), clock);
        Assert.Equal(SubscriptionTier.Free, tamperedSvc.GetCurrentEntitlement().ActiveTier);

        // Valid device + valid token -> ProPerpetual
        store.Save(new StoredLicenseState(validToken, "sess", "a@b.com", now));
        var okSvc = new SignedTokenEntitlementService(store, _verifier, new FixedFingerprintProvider("sha256:device-1"), clock);
        Assert.Equal(SubscriptionTier.ProPerpetual, okSvc.GetCurrentEntitlement().ActiveTier);
    }

    [Fact]
    public void DpapiLicenseStore_RoundTripsAndFailsClosedOnCorruption()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var path = Path.Combine(_tempDir, "license.dpapi");
        var store = new DpapiLicenseStore(path, new WindowsDpapiProtector());
        var now = new DateTimeOffset(2026, 11, 26, 6, 0, 0, TimeSpan.Zero);

        var expected = new StoredLicenseState("tok.sig", "sess-123", "buyer@switchly.id", now);
        store.Save(expected);

        var loaded = store.Load();
        Assert.Equal(expected, loaded);

        // Corrupt file -> Load returns null (fail-closed)
        File.WriteAllBytes(path, [0x01, 0x02, 0x03, 0x04]);
        Assert.Null(store.Load());

        store.Clear();
        Assert.False(File.Exists(path));
    }

    private string Sign(SignedLicensePayload payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload);
        var payloadPart = Convert.ToBase64String(json).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var payloadBytes = Encoding.ASCII.GetBytes(payloadPart);

        var signer = new Ed25519Signer();
        signer.Init(true, _privateKey);
        signer.BlockUpdate(payloadBytes, 0, payloadBytes.Length);
        var sig = signer.GenerateSignature();
        var sigPart = Convert.ToBase64String(sig).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        return $"{payloadPart}.{sigPart}";
    }

    private sealed class InMemoryLicenseStore : ILicenseStore
    {
        private StoredLicenseState? _state;
        public StoredLicenseState? Load() => _state;
        public void Save(StoredLicenseState state) => _state = state;
        public void Clear() => _state = null;
    }

    private sealed class FixedFingerprintProvider(string hash) : IDeviceFingerprintProvider
    {
        public string GetDeviceHash() => hash;
    }

    private sealed class FixedTimeProvider(DateTimeOffset currentUtc) : TimeProvider
    {
        public DateTimeOffset CurrentUtc { get; set; } = currentUtc;
        public override DateTimeOffset GetUtcNow() => CurrentUtc;
    }
}
