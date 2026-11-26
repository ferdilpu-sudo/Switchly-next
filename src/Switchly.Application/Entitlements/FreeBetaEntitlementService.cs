using Switchly.Domain.Entitlements;

namespace Switchly.Application.Entitlements;

/// <summary>
/// Free Beta entitlement implementation (D8, ADR-009, REQ-012, REQ-013):
/// runs fully offline with no registration requirement and no artificial profile quotas.
/// </summary>
public sealed class FreeBetaEntitlementService : IFeatureEntitlementService
{
    private static readonly EntitlementSnapshot Snapshot = new(
        ActiveTier: SubscriptionTier.FreeBeta,
        RequiresAccount: false,
        MaxProfiles: null,
        IsOfflineReady: true,
        DisplayBadge: "Free Beta");

    public EntitlementSnapshot GetCurrentEntitlement() => Snapshot;

    public FeatureEntitlementDecision Evaluate(FeatureGateKey feature)
    {
        return feature switch
        {
            FeatureGateKey.ProfileManagement
                or FeatureGateKey.MultiWindowAndTabs
                or FeatureGateKey.BrowserImport
                or FeatureGateKey.PasswordVault
                or FeatureGateKey.ManualEncryptedBackup =>
                new FeatureEntitlementDecision(true, SubscriptionTier.FreeBeta),

            FeatureGateKey.ScheduledBackup =>
                new FeatureEntitlementDecision(
                    false,
                    SubscriptionTier.FreeBeta,
                    "Backup terjadwal otomatis ditunda ke rilis setelah Beta (keputusan D2)."),

            FeatureGateKey.OptionalExtensions =>
                new FeatureEntitlementDecision(
                    false,
                    SubscriptionTier.FreeBeta,
                    "Dukungan ekstensi bersifat opsional dan ditunda ke rilis setelah Beta (M-005)."),

            _ => new FeatureEntitlementDecision(
                false,
                SubscriptionTier.FreeBeta,
                "Fitur belum tersedia pada rilis Beta ini."),
        };
    }

    public FeatureEntitlementDecision CanCreateProfile(int currentActiveProfileCount)
    {
        if (currentActiveProfileCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentActiveProfileCount));
        }

        // No artificial profile cap in Free Beta (D8 / REQ-012 / TEST-015).
        return new FeatureEntitlementDecision(true, SubscriptionTier.FreeBeta);
    }

    public FeatureEntitlementDecision CanOpenProfile(Guid profileId, IReadOnlyList<Guid> orderedActiveProfileIds)
    {
        return new FeatureEntitlementDecision(true, SubscriptionTier.FreeBeta);
    }
}
