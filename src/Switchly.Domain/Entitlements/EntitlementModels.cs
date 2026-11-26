using System.Text.Json.Serialization;

namespace Switchly.Domain.Entitlements;

public enum SubscriptionTier
{
    FreeBeta = 0,
    Pro = 1,
    Free = 2,
    TrialPro = 3,
    ProSubscription = 4,
    ProPerpetual = 5,
}

public enum FeatureGateKey
{
    ProfileManagement = 0,
    MultiWindowAndTabs = 1,
    BrowserImport = 2,
    PasswordVault = 3,
    ManualEncryptedBackup = 4,
    ScheduledBackup = 5,
    OptionalExtensions = 6,
}

public sealed record FeatureEntitlementDecision(
    bool IsAllowed,
    SubscriptionTier ActiveTier,
    string? Reason = null);

public sealed record EntitlementSnapshot(
    SubscriptionTier ActiveTier,
    bool RequiresAccount,
    int? MaxProfiles,
    bool IsOfflineReady,
    string DisplayBadge,
    DateTimeOffset? ExpiresAtUtc = null,
    DateTimeOffset? UpdatesUntilUtc = null,
    bool NeedsOnlineRefresh = false);

public sealed record SignedLicensePayload(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("sub")] string AccountId,
    [property: JsonPropertyName("tier")] string Tier,
    [property: JsonPropertyName("max_profiles")] int? MaxProfiles,
    [property: JsonPropertyName("device_hash")] string DeviceHash,
    [property: JsonPropertyName("issued_at")] DateTimeOffset IssuedAtUtc,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAtUtc,
    [property: JsonPropertyName("updates_until")] DateTimeOffset? UpdatesUntilUtc,
    [property: JsonPropertyName("refresh_after")] DateTimeOffset RefreshAfterUtc,
    [property: JsonPropertyName("offline_grace_until")] DateTimeOffset OfflineGraceUntilUtc);

public sealed record StoredLicenseState(
    string SignedToken,
    string? SessionToken,
    string? AccountEmail,
    DateTimeOffset LastObservedUtc);
