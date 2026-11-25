namespace Switchly.Domain.Entitlements;

public enum SubscriptionTier
{
    FreeBeta = 0,
    Pro = 1,
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
    string DisplayBadge);
