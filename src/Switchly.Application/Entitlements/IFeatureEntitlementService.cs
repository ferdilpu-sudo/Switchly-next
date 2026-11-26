using Switchly.Domain.Entitlements;

namespace Switchly.Application.Entitlements;

public interface IFeatureEntitlementService
{
    EntitlementSnapshot GetCurrentEntitlement();

    FeatureEntitlementDecision Evaluate(FeatureGateKey feature);

    FeatureEntitlementDecision CanCreateProfile(int currentActiveProfileCount);

    /// <summary>
    /// Supports non-destructive graceful downgrade (D16): when a Trial/Pro plan expires,
    /// existing profiles beyond the Free quota are not deleted; only the first N profiles
    /// remain openable until the user upgrades or archives excess profiles.
    /// </summary>
    FeatureEntitlementDecision CanOpenProfile(Guid profileId, IReadOnlyList<Guid> orderedActiveProfileIds);
}

public interface ILicenseTokenVerifier
{
    bool TryVerify(string signedToken, out SignedLicensePayload? payload);
}

public interface ILicenseStore
{
    StoredLicenseState? Load();

    void Save(StoredLicenseState state);

    void Clear();
}

public interface IDeviceFingerprintProvider
{
    string GetDeviceHash();
}
