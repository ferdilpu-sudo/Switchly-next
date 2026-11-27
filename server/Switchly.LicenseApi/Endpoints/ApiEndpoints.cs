using Microsoft.EntityFrameworkCore;
using Switchly.LicenseApi.Crypto;
using Switchly.LicenseApi.Data;
using Switchly.LicenseApi.Services;

namespace Switchly.LicenseApi.Endpoints;

public sealed record OtpRequestDto(string? Email);
public sealed record OtpVerifyDto(string? Email, string? Code, string? DeviceHash, string? DeviceLabel);
public sealed record DeviceActionDto(string? DeviceHash, string? DeviceLabel);
public sealed record CheckoutRequestDto(string? PlanCode);

public static class ApiEndpoints
{
    public const string OtpRequestRateLimitPolicy = "otp-request";
    public const string OtpVerifyRateLimitPolicy = "otp-verify";
    public const string CheckoutRateLimitPolicy = "checkout";

    public static void MapLicenseApiEndpoints(this WebApplication app)
    {
        app.MapGet("/healthz", async (LicenseDbContext db, IEd25519TokenService tokens, CancellationToken ct) =>
        {
            var canConnect = await db.Database.CanConnectAsync(ct);
            return canConnect
                ? Results.Ok(new { status = "ok", public_key = tokens.PublicKeyBase64 })
                : Results.Problem("Database unavailable", statusCode: StatusCodes.Status503ServiceUnavailable);
        });

        var v1 = app.MapGroup("/v1");

        v1.MapGet("/plans", () => Results.Ok(PlanCodes.Catalog.Values.Select(p => new
        {
            code = p.Code,
            name = p.DisplayName,
            price_idr = p.PriceIdr,
            duration_days = p.DurationDays,
            updates_months = p.UpdatesMonths,
            max_devices = p.MaxDevices,
        })));

        v1.MapPost("/auth/otp/request", async (OtpRequestDto? dto, OtpService otpService, CancellationToken ct) =>
        {
            var result = await otpService.RequestOtpAsync(dto?.Email, ct);
            return result.Succeeded
                ? Results.Ok(new { message = "Kode OTP telah dikirim ke email Anda." })
                : Results.BadRequest(new { error = result.ErrorMessage });
        }).RequireRateLimiting(OtpRequestRateLimitPolicy);

        v1.MapPost("/auth/otp/verify", async (
            OtpVerifyDto? dto,
            OtpService otpService,
            EntitlementIssuer issuer,
            CancellationToken ct) =>
        {
            if (dto is null)
            {
                return Results.BadRequest(new { error = "Payload tidak valid." });
            }

            var verify = await otpService.VerifyOtpAsync(dto.Email, dto.Code, dto.DeviceHash, ct);
            if (!verify.Succeeded || verify.Account is null || verify.SessionToken is null)
            {
                return Results.BadRequest(new { error = verify.ErrorMessage });
            }

            var issued = await issuer.IssueForDeviceAsync(
                verify.Account,
                dto.DeviceHash,
                dto.DeviceLabel ?? "Windows PC",
                ct);

            if (!issued.Succeeded)
            {
                return Results.Conflict(new
                {
                    error = issued.ErrorMessage,
                    session_token = verify.SessionToken,
                });
            }

            return Results.Ok(new
            {
                session_token = verify.SessionToken,
                entitlement_token = issued.EntitlementToken,
                tier = issued.Payload?.Tier,
                trial_eligible = issued.TrialEligible,
            });
        }).RequireRateLimiting(OtpVerifyRateLimitPolicy);

        v1.MapPost("/auth/logout", async (
            HttpRequest httpRequest,
            OtpService otpService,
            LicenseDbContext db,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var session = await AuthenticateAsync(httpRequest, otpService, ct);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            session.RevokedAtUtc = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Sesi berhasil diakhiri." });
        });

        v1.MapPost("/trial/start", async (
            HttpRequest httpRequest,
            DeviceActionDto? dto,
            OtpService otpService,
            EntitlementIssuer issuer,
            CancellationToken ct) =>
        {
            var session = await AuthenticateAsync(httpRequest, otpService, ct);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            // Always bind trial claims to the authenticated session's verified DeviceHash (#5).
            var result = await issuer.StartTrialAsync(
                session.Account,
                session.DeviceHash,
                dto?.DeviceLabel ?? "Windows PC",
                ct);

            return result.Succeeded
                ? Results.Ok(new
                {
                    entitlement_token = result.EntitlementToken,
                    trial_expires_at_utc = result.TrialExpiresAtUtc,
                })
                : Results.BadRequest(new { error = result.ErrorMessage });
        });

        v1.MapPost("/entitlement/refresh", async (
            HttpRequest httpRequest,
            DeviceActionDto? dto,
            OtpService otpService,
            EntitlementIssuer issuer,
            CancellationToken ct) =>
        {
            var session = await AuthenticateAsync(httpRequest, otpService, ct);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            // Always bind entitlement refresh to the authenticated session's verified DeviceHash (#5).
            var issued = await issuer.IssueForDeviceAsync(
                session.Account,
                session.DeviceHash,
                dto?.DeviceLabel ?? "Windows PC",
                ct);

            return issued.Succeeded
                ? Results.Ok(new
                {
                    entitlement_token = issued.EntitlementToken,
                    tier = issued.Payload?.Tier,
                    expires_at_utc = issued.Payload?.ExpiresAtUtc,
                    updates_until_utc = issued.Payload?.UpdatesUntilUtc,
                    trial_eligible = issued.TrialEligible,
                })
                : Results.Conflict(new { error = issued.ErrorMessage });
        });

        v1.MapGet("/devices", async (
            HttpRequest httpRequest,
            OtpService otpService,
            LicenseDbContext db,
            CancellationToken ct) =>
        {
            var session = await AuthenticateAsync(httpRequest, otpService, ct);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var allDevices = await db.DeviceActivations
                .Where(d => d.AccountId == session.AccountId && d.RevokedAtUtc == null)
                .ToListAsync(ct);

            var devices = allDevices
                .OrderByDescending(d => d.LastSeenUtc)
                .Select(d => new
                {
                    id = d.Id,
                    device_label = d.DeviceLabel,
                    activated_at_utc = d.ActivatedAtUtc,
                    last_seen_utc = d.LastSeenUtc,
                    is_current_session = d.DeviceHash == session.DeviceHash,
                })
                .ToList();

            return Results.Ok(devices);
        });

        v1.MapDelete("/devices/{deviceId:guid}", async (
            Guid deviceId,
            HttpRequest httpRequest,
            OtpService otpService,
            LicenseDbContext db,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var session = await AuthenticateAsync(httpRequest, otpService, ct);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var device = await db.DeviceActivations
                .FirstOrDefaultAsync(d => d.Id == deviceId && d.AccountId == session.AccountId && d.RevokedAtUtc == null, ct);

            if (device is null)
            {
                return Results.NotFound(new { error = "Perangkat tidak ditemukan." });
            }

            var now = timeProvider.GetUtcNow();
            device.RevokedAtUtc = now;

            // Also revoke all active bearer sessions bound to that device so a revoked PC cannot silently re-activate (S1-02).
            var deviceSessions = await db.Sessions
                .Where(s => s.AccountId == session.AccountId && s.DeviceHash == device.DeviceHash && s.RevokedAtUtc == null)
                .ToListAsync(ct);
            foreach (var s in deviceSessions)
            {
                s.RevokedAtUtc = now;
            }

            await db.SaveChangesAsync(ct);
            return Results.Ok(new { message = "Perangkat dan sesi terkait berhasil dinonaktifkan." });
        });

        v1.MapPost("/billing/checkout", async (
            HttpRequest httpRequest,
            CheckoutRequestDto? dto,
            OtpService otpService,
            IMidtransService midtrans,
            CancellationToken ct) =>
        {
            var session = await AuthenticateAsync(httpRequest, otpService, ct);
            if (session is null)
            {
                return Results.Unauthorized();
            }

            var checkout = await midtrans.CreateSnapCheckoutAsync(session.Account, dto?.PlanCode, ct);
            return checkout.Succeeded
                ? Results.Ok(new
                {
                    order_id = checkout.OrderId,
                    snap_token = checkout.SnapToken,
                    checkout_url = checkout.RedirectUrl,
                })
                : Results.BadRequest(new { error = checkout.ErrorMessage });
        }).RequireRateLimiting(CheckoutRateLimitPolicy);

        v1.MapPost("/webhooks/payment/midtrans", async (
            MidtransWebhookPayload payload,
            IMidtransService midtrans,
            CancellationToken ct) =>
        {
            var result = await midtrans.ProcessWebhookAsync(payload, ct);
            return Results.Json(new { message = result.Message }, statusCode: result.HttpStatusCode);
        });
    }

    private static async Task<SessionRecord?> AuthenticateAsync(
        HttpRequest request,
        OtpService otpService,
        CancellationToken ct)
    {
        var authHeader = request.Headers.Authorization.ToString();
        if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = authHeader["Bearer ".Length..].Trim();
        return await otpService.ValidateSessionAsync(token, ct);
    }
}
