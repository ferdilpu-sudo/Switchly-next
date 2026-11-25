using Switchly.Application.Entitlements;
using Switchly.Domain.Entitlements;

namespace Switchly.Application.Tests;

public sealed class FreeBetaEntitlementServiceTests
{
    [Fact]
    public void GetCurrentEntitlement_ReturnsOfflineFreeBetaWithoutAccountOrQuota()
    {
        var service = new FreeBetaEntitlementService();

        var snapshot = service.GetCurrentEntitlement();

        Assert.Equal(SubscriptionTier.FreeBeta, snapshot.ActiveTier);
        Assert.False(snapshot.RequiresAccount);
        Assert.Null(snapshot.MaxProfiles);
        Assert.True(snapshot.IsOfflineReady);
        Assert.Equal("Free Beta", snapshot.DisplayBadge);
    }

    [Theory]
    [InlineData(FeatureGateKey.ProfileManagement)]
    [InlineData(FeatureGateKey.MultiWindowAndTabs)]
    [InlineData(FeatureGateKey.BrowserImport)]
    [InlineData(FeatureGateKey.PasswordVault)]
    [InlineData(FeatureGateKey.ManualEncryptedBackup)]
    public void Evaluate_AllowsAllBetaMvpFeaturesWithoutLicenseBackend(FeatureGateKey feature)
    {
        var service = new FreeBetaEntitlementService();

        var decision = service.Evaluate(feature);

        Assert.True(decision.IsAllowed);
        Assert.Equal(SubscriptionTier.FreeBeta, decision.ActiveTier);
        Assert.Null(decision.Reason);
    }

    [Theory]
    [InlineData(FeatureGateKey.ScheduledBackup)]
    [InlineData(FeatureGateKey.OptionalExtensions)]
    public void Evaluate_ExplicitlyDocumentsDeferredPostBetaFeatures(FeatureGateKey feature)
    {
        var service = new FreeBetaEntitlementService();

        var decision = service.Evaluate(feature);

        Assert.False(decision.IsAllowed);
        Assert.Equal(SubscriptionTier.FreeBeta, decision.ActiveTier);
        Assert.False(string.IsNullOrWhiteSpace(decision.Reason));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(10)]
    [InlineData(250)]
    public void CanCreateProfile_HasNoArtificialQuotaInFreeBeta(int activeProfiles)
    {
        var service = new FreeBetaEntitlementService();

        var decision = service.CanCreateProfile(activeProfiles);

        Assert.True(decision.IsAllowed);
        Assert.Equal(SubscriptionTier.FreeBeta, decision.ActiveTier);
    }

    [Fact]
    public void CanCreateProfile_ThrowsOnNegativeCount()
    {
        var service = new FreeBetaEntitlementService();

        Assert.Throws<ArgumentOutOfRangeException>(() => service.CanCreateProfile(-1));
    }
}
