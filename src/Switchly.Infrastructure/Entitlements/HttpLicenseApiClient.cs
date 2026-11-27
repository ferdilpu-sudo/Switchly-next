using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Switchly.Application.Entitlements;

namespace Switchly.Infrastructure.Entitlements;

/// <summary>
/// HTTPS client for Switchly.LicenseApi hosted on Oracle Cloud Always Free Ampere A1 (D16).
/// </summary>
public sealed class HttpLicenseApiClient : ILicenseApiClient, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public HttpLicenseApiClient(LicenseClientConfig config)
    {
        _baseUrl = (config.ApiBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        IsConfigured = config.IsConfigured && Uri.TryCreate(_baseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.IsLoopback);

        _httpClient = new HttpClient(new SocketsHttpHandler { AllowAutoRedirect = false })
        {
            Timeout = TimeSpan.FromSeconds(15),
            MaxResponseContentBufferSize = 256 * 1024,
        };
    }

    public bool IsConfigured { get; }

    public void Dispose() => _httpClient.Dispose();

    public async Task<LicenseApiOutcome> RequestOtpAsync(string email, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new LicenseApiOutcome(false, "Server lisensi belum dikonfigurasi pada build ini.");
        }

        try
        {
            using var resp = await PostJsonAsync("/v1/auth/otp/request", new { email }, null, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return resp.IsSuccessStatusCode
                ? new LicenseApiOutcome(true, ExtractString(body, "message") ?? "Kode OTP terkirim.")
                : new LicenseApiOutcome(false, ExtractString(body, "error") ?? $"Gagal meminta OTP ({(int)resp.StatusCode}).");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new LicenseApiOutcome(false, "Tidak dapat terhubung ke server lisensi. Periksa koneksi internet Anda.");
        }
    }

    public async Task<OtpVerifyApiResult> VerifyOtpAsync(
        string email,
        string code,
        string deviceHash,
        string deviceLabel,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new OtpVerifyApiResult(false, ErrorMessage: "Server lisensi belum dikonfigurasi.");
        }

        try
        {
            using var resp = await PostJsonAsync(
                "/v1/auth/otp/verify",
                new { email, code, deviceHash, deviceLabel },
                null,
                ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new OtpVerifyApiResult(false, ErrorMessage: ExtractString(body, "error") ?? "Verifikasi OTP gagal.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new OtpVerifyApiResult(
                Succeeded: true,
                SessionToken: TryGetString(root, "session_token"),
                EntitlementToken: TryGetString(root, "entitlement_token"),
                TrialEligible: root.TryGetProperty("trial_eligible", out var te) && te.ValueKind == JsonValueKind.True);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new OtpVerifyApiResult(false, ErrorMessage: "Gagal menghubungi server lisensi.");
        }
    }

    public async Task<TrialStartApiResult> StartTrialAsync(
        string sessionToken,
        string deviceHash,
        string deviceLabel,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new TrialStartApiResult(false, ErrorMessage: "Server lisensi belum dikonfigurasi.");
        }

        try
        {
            using var resp = await PostJsonAsync(
                "/v1/trial/start",
                new { deviceHash, deviceLabel },
                sessionToken,
                ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new TrialStartApiResult(false, ErrorMessage: ExtractString(body, "error") ?? "Gagal mengaktifkan Trial Pro.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            DateTimeOffset? expires = root.TryGetProperty("trial_expires_at_utc", out var exp)
                && exp.TryGetDateTimeOffset(out var dto) ? dto : null;

            return new TrialStartApiResult(
                Succeeded: true,
                EntitlementToken: TryGetString(root, "entitlement_token"),
                TrialExpiresAtUtc: expires);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new TrialStartApiResult(false, ErrorMessage: "Gagal menghubungi server lisensi.");
        }
    }

    public async Task<RefreshApiResult> RefreshEntitlementAsync(
        string sessionToken,
        string deviceHash,
        string deviceLabel,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new RefreshApiResult(false, ErrorMessage: "Server lisensi belum dikonfigurasi.");
        }

        try
        {
            using var resp = await PostJsonAsync(
                "/v1/entitlement/refresh",
                new { deviceHash, deviceLabel },
                sessionToken,
                ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new RefreshApiResult(false, ErrorMessage: ExtractString(body, "error") ?? "Gagal memperbarui status lisensi.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new RefreshApiResult(
                Succeeded: true,
                EntitlementToken: TryGetString(root, "entitlement_token"),
                TrialEligible: root.TryGetProperty("trial_eligible", out var te) && te.ValueKind == JsonValueKind.True);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new RefreshApiResult(false, ErrorMessage: "Tidak dapat terhubung ke server lisensi.");
        }
    }

    public async Task<CheckoutApiResult> CreateCheckoutAsync(
        string sessionToken,
        string planCode,
        CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new CheckoutApiResult(false, ErrorMessage: "Server lisensi belum dikonfigurasi.");
        }

        try
        {
            using var resp = await PostJsonAsync(
                "/v1/billing/checkout",
                new { planCode },
                sessionToken,
                ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new CheckoutApiResult(false, ErrorMessage: ExtractString(body, "error") ?? "Gagal membuat link pembayaran Midtrans.");
            }

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new CheckoutApiResult(
                Succeeded: true,
                OrderId: TryGetString(root, "order_id"),
                CheckoutUrl: TryGetString(root, "checkout_url"));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            return new CheckoutApiResult(false, ErrorMessage: "Gagal membuat transaksi Midtrans.");
        }
    }

    public async Task<IReadOnlyList<ActiveDeviceDto>> ListDevicesAsync(string sessionToken, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return Array.Empty<ActiveDeviceDto>();
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/v1/devices");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
            using var resp = await _httpClient.SendAsync(req, ct);
            if (!resp.IsSuccessStatusCode)
            {
                return Array.Empty<ActiveDeviceDto>();
            }

            var body = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var list = new List<ActiveDeviceDto>();
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in doc.RootElement.EnumerateArray())
                {
                    if (item.TryGetProperty("id", out var idProp) && idProp.TryGetGuid(out var id))
                    {
                        list.Add(new ActiveDeviceDto(
                            Id: id,
                            DeviceLabel: TryGetString(item, "device_label") ?? "Windows PC",
                            ActivatedAtUtc: item.TryGetProperty("activated_at_utc", out var act) && act.TryGetDateTimeOffset(out var actDto) ? actDto : DateTimeOffset.MinValue,
                            LastSeenUtc: item.TryGetProperty("last_seen_utc", out var seen) && seen.TryGetDateTimeOffset(out var seenDto) ? seenDto : DateTimeOffset.MinValue,
                            IsCurrentSession: item.TryGetProperty("is_current_session", out var cur) && cur.ValueKind == JsonValueKind.True));
                    }
                }
            }

            return list;
        }
        catch
        {
            return Array.Empty<ActiveDeviceDto>();
        }
    }

    public async Task<LicenseApiOutcome> RevokeDeviceAsync(string sessionToken, Guid deviceId, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new LicenseApiOutcome(false, "Server lisensi belum dikonfigurasi.");
        }

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Delete, $"{_baseUrl}/v1/devices/{deviceId}");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", sessionToken);
            using var resp = await _httpClient.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            return resp.IsSuccessStatusCode
                ? new LicenseApiOutcome(true, ExtractString(body, "message") ?? "Perangkat dinonaktifkan.")
                : new LicenseApiOutcome(false, ExtractString(body, "error") ?? "Gagal menonaktifkan perangkat.");
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new LicenseApiOutcome(false, "Tidak dapat terhubung ke server lisensi.");
        }
    }

    public async Task LogoutAsync(string sessionToken, CancellationToken ct = default)
    {
        if (!IsConfigured || string.IsNullOrWhiteSpace(sessionToken))
        {
            return;
        }

        try
        {
            using var resp = await PostJsonAsync("/v1/auth/logout", new { }, sessionToken, ct);
        }
        catch
        {
        }
    }

    // Compile-time pinned GA trust anchor (#6). Populate EmbeddedPublicKeyBase64 and set
    // EmbeddedEnforceCommercialLimits = true when cutting the commercial GA release.
    private const string EmbeddedApiBaseUrl = "https://api.switchly.site";
    private const string EmbeddedPublicKeyBase64 = "";
    private const bool EmbeddedEnforceCommercialLimits = false;

    public static LicenseClientConfig LoadConfigFromDiskOrEnvironment(string dataDirectory)
    {
#if DEBUG
        // Allow local/staging overrides only in DEBUG builds so Release builds cannot have their
        // Ed25519 trust anchor or enforcement flag tampered via %LOCALAPPDATA% or env vars (#6).
        var envUrl = Environment.GetEnvironmentVariable("SWITCHLY_LICENSE_API_URL");
        var envPub = Environment.GetEnvironmentVariable("SWITCHLY_ED25519_PUBLIC_KEY");
        var envEnforce = string.Equals(
            Environment.GetEnvironmentVariable("SWITCHLY_ENFORCE_LICENSING"),
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(envUrl) && !string.IsNullOrWhiteSpace(envPub))
        {
            return new LicenseClientConfig(envUrl.Trim(), envPub.Trim(), envEnforce);
        }

        var configPath = Path.Combine(dataDirectory, "license-config.json");
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                var url = TryGetString(root, "api_base_url") ?? string.Empty;
                var pub = TryGetString(root, "ed25519_public_key") ?? string.Empty;
                var enforce = root.TryGetProperty("enforce_commercial_limits", out var enf)
                    && enf.ValueKind == JsonValueKind.True;
                return new LicenseClientConfig(url, pub, enforce);
            }
            catch
            {
                // Ignore malformed optional local config and remain in Free Beta mode.
            }
        }
#endif

        return new LicenseClientConfig(EmbeddedApiBaseUrl, EmbeddedPublicKeyBase64, EmbeddedEnforceCommercialLimits);
    }

    private async Task<HttpResponseMessage> PostJsonAsync(string path, object body, string? bearerToken, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}{path}")
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
        };

        if (!string.IsNullOrWhiteSpace(bearerToken))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        }

        return await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
    }

    private static string? ExtractString(string json, string propertyName)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return TryGetString(doc.RootElement, propertyName);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? TryGetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String
            ? prop.GetString()
            : null;
}
