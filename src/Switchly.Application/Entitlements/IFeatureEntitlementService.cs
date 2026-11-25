using Switchly.Domain.Entitlements;

namespace Switchly.Application.Entitlements;

public interface IFeatureEntitlementService
{
    EntitlementSnapshot GetCurrentEntitlement();

    FeatureEntitlementDecision Evaluate(FeatureGateKey feature);

    FeatureEntitlementDecision CanCreateProfile(int currentActiveProfileCount);
}
