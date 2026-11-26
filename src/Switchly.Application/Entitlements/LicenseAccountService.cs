using Switchly.Domain.Entitlements;

namespace Switchly.Application.Entitlements;

public sealed record LicenseClientConfig(
    string ApiBaseUrl,
    string Ed25519PublicKeyBase64,
    bool EnforceCommercialLimits)
{
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiBaseUrl)
        && !string.IsNullOrWhiteSpace(Ed25519PublicKeyBase64);
}

public sealed record LicenseApiOutcome(bool Succeeded, string? Message = null);

public sealed record OtpVerifyApiResult(
    bool Succeeded,
    string? SessionToken = null,
    string? EntitlementToken = null,
    bool TrialEligible = false,
    string? ErrorMessage = null);

public sealed record TrialStartApiResult(
    bool Succeeded,
    string? EntitlementToken = null,
    DateTimeOffset? TrialExpiresAtUtc = null,
    string? ErrorMessage = null);

public sealed record RefreshApiResult(
    bool Succeeded,
    string? EntitlementToken = null,
    bool TrialEligible = false,
    string? ErrorMessage = null);

public sealed record CheckoutApiResult(
    bool Succeeded,
    string? OrderId = null,
    string? CheckoutUrl = null,
    string? ErrorMessage = null);

public sealed record ActiveDeviceDto(
    Guid Id,
    string DeviceLabel,
    DateTimeOffset ActivatedAtUtc,
    DateTimeOffset LastSeenUtc,
    bool IsCurrentSession);

public interface ILicenseApiClient
{
    bool IsConfigured { get; }

    Task<LicenseApiOutcome> RequestOtpAsync(string email, CancellationToken ct = default);

    Task<OtpVerifyApiResult> VerifyOtpAsync(string email, string code, string deviceHash, string deviceLabel, CancellationToken ct = default);

    Task<TrialStartApiResult> StartTrialAsync(string sessionToken, string deviceHash, string deviceLabel, CancellationToken ct = default);

    Task<RefreshApiResult> RefreshEntitlementAsync(string sessionToken, string deviceHash, string deviceLabel, CancellationToken ct = default);

    Task<CheckoutApiResult> CreateCheckoutAsync(string sessionToken, string planCode, CancellationToken ct = default);

    Task<IReadOnlyList<ActiveDeviceDto>> ListDevicesAsync(string sessionToken, CancellationToken ct = default);

    Task<LicenseApiOutcome> RevokeDeviceAsync(string sessionToken, Guid deviceId, CancellationToken ct = default);
}

public sealed class LicenseAccountService
{
    private readonly ILicenseApiClient _apiClient;
    private readonly ILicenseStore _licenseStore;
    private readonly IDeviceFingerprintProvider _fingerprintProvider;
    private readonly IFeatureEntitlementService _entitlementService;
    private readonly TimeProvider _timeProvider;

    public LicenseAccountService(
        ILicenseApiClient apiClient,
        ILicenseStore licenseStore,
        IDeviceFingerprintProvider fingerprintProvider,
        IFeatureEntitlementService entitlementService,
        TimeProvider? timeProvider = null)
    {
        _apiClient = apiClient;
        _licenseStore = licenseStore;
        _fingerprintProvider = fingerprintProvider;
        _entitlementService = entitlementService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public bool IsBackendConfigured => _apiClient.IsConfigured;

    public EntitlementSnapshot GetSnapshot() => _entitlementService.GetCurrentEntitlement();

    public StoredLicenseState? GetStoredState() => _licenseStore.Load();

    public string GetDeviceLabel() => Environment.MachineName;

    public Task<LicenseApiOutcome> RequestOtpAsync(string email, CancellationToken ct = default)
        => _apiClient.RequestOtpAsync(email, ct);

    public async Task<OtpVerifyApiResult> VerifyOtpAsync(string email, string code, CancellationToken ct = default)
    {
        var deviceHash = _fingerprintProvider.GetDeviceHash();
        var result = await _apiClient.VerifyOtpAsync(email, code, deviceHash, GetDeviceLabel(), ct);
        if (result.Succeeded && !string.IsNullOrWhiteSpace(result.EntitlementToken))
        {
            _licenseStore.Save(new StoredLicenseState(
                SignedToken: result.EntitlementToken,
                SessionToken: result.SessionToken,
                AccountEmail: email.Trim().ToLowerInvariant(),
                LastObservedUtc: _timeProvider.GetUtcNow()));
        }

        return result;
    }

    public async Task<TrialStartApiResult> StartTrialAsync(CancellationToken ct = default)
    {
        var stored = _licenseStore.Load();
        if (stored is null || string.IsNullOrWhiteSpace(stored.SessionToken))
        {
            return new TrialStartApiResult(false, ErrorMessage: "Masuk dengan email OTP terlebih dahulu untuk mengaktifkan Trial Pro 7 hari.");
        }

        var result = await _apiClient.StartTrialAsync(
            stored.SessionToken,
            _fingerprintProvider.GetDeviceHash(),
            GetDeviceLabel(),
            ct);

        if (result.Succeeded && !string.IsNullOrWhiteSpace(result.EntitlementToken))
        {
            _licenseStore.Save(stored with
            {
                SignedToken = result.EntitlementToken,
                LastObservedUtc = _timeProvider.GetUtcNow(),
            });
        }

        return result;
    }

    public async Task<RefreshApiResult> RefreshAsync(CancellationToken ct = default)
    {
        var stored = _licenseStore.Load();
        if (stored is null || string.IsNullOrWhiteSpace(stored.SessionToken))
        {
            return new RefreshApiResult(false, ErrorMessage: "Belum ada akun Switchly yang masuk.");
        }

        var result = await _apiClient.RefreshEntitlementAsync(
            stored.SessionToken,
            _fingerprintProvider.GetDeviceHash(),
            GetDeviceLabel(),
            ct);

        if (result.Succeeded && !string.IsNullOrWhiteSpace(result.EntitlementToken))
        {
            _licenseStore.Save(stored with
            {
                SignedToken = result.EntitlementToken,
                LastObservedUtc = _timeProvider.GetUtcNow(),
            });
        }

        return result;
    }

    public async Task<CheckoutApiResult> StartCheckoutAsync(string planCode, CancellationToken ct = default)
    {
        var stored = _licenseStore.Load();
        if (stored is null || string.IsNullOrWhiteSpace(stored.SessionToken))
        {
            return new CheckoutApiResult(false, ErrorMessage: "Masuk dengan email OTP terlebih dahulu sebelum checkout.");
        }

        var result = await _apiClient.CreateCheckoutAsync(stored.SessionToken, planCode, ct);
        if (result.Succeeded)
        {
            if (!Uri.TryCreate(result.CheckoutUrl, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return new CheckoutApiResult(false, ErrorMessage: "URL checkout tidak valid (wajib HTTPS).");
            }
        }

        return result;
    }

    public Task<IReadOnlyList<ActiveDeviceDto>> ListDevicesAsync(CancellationToken ct = default)
    {
        var stored = _licenseStore.Load();
        if (stored is null || string.IsNullOrWhiteSpace(stored.SessionToken))
        {
            return Task.FromResult<IReadOnlyList<ActiveDeviceDto>>(Array.Empty<ActiveDeviceDto>());
        }

        return _apiClient.ListDevicesAsync(stored.SessionToken, ct);
    }

    public Task<LicenseApiOutcome> RevokeDeviceAsync(Guid deviceId, CancellationToken ct = default)
    {
        var stored = _licenseStore.Load();
        if (stored is null || string.IsNullOrWhiteSpace(stored.SessionToken))
        {
            return Task.FromResult(new LicenseApiOutcome(false, "Belum ada akun yang masuk."));
        }

        return _apiClient.RevokeDeviceAsync(stored.SessionToken, deviceId, ct);
    }

    public void SignOut() => _licenseStore.Clear();
}
