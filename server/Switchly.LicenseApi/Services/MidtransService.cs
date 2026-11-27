using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Switchly.LicenseApi.Data;
using Switchly.LicenseApi.Options;

namespace Switchly.LicenseApi.Services;

public sealed record MidtransCheckoutResult(
    bool Succeeded,
    string? OrderId = null,
    string? SnapToken = null,
    string? RedirectUrl = null,
    string? ErrorMessage = null);

public sealed record MidtransWebhookPayload(
    [property: JsonPropertyName("order_id")] string? OrderId,
    [property: JsonPropertyName("status_code")] string? StatusCode,
    [property: JsonPropertyName("gross_amount")] string? GrossAmount,
    [property: JsonPropertyName("signature_key")] string? SignatureKey,
    [property: JsonPropertyName("transaction_status")] string? TransactionStatus,
    [property: JsonPropertyName("fraud_status")] string? FraudStatus,
    [property: JsonPropertyName("currency")] string? Currency = null);

public sealed record MidtransWebhookResult(bool Succeeded, int HttpStatusCode, string Message);

public interface IMidtransService
{
    bool VerifyWebhookSignature(string? orderId, string? statusCode, string? grossAmount, string? signatureKey);

    Task<MidtransCheckoutResult> CreateSnapCheckoutAsync(Account account, string? planCode, CancellationToken ct);

    Task<MidtransWebhookResult> ProcessWebhookAsync(MidtransWebhookPayload payload, CancellationToken ct);
}

public sealed class MidtransService(
    HttpClient httpClient,
    LicenseDbContext db,
    IOptions<MidtransOptions> options,
    TimeProvider timeProvider,
    ILogger<MidtransService> logger) : IMidtransService
{
    private readonly MidtransOptions _options = options.Value;

    public bool VerifyWebhookSignature(string? orderId, string? statusCode, string? grossAmount, string? signatureKey)
    {
        if (string.IsNullOrWhiteSpace(_options.ServerKey)
            || string.IsNullOrWhiteSpace(orderId)
            || string.IsNullOrWhiteSpace(statusCode)
            || string.IsNullOrWhiteSpace(grossAmount)
            || string.IsNullOrWhiteSpace(signatureKey))
        {
            return false;
        }

        var raw = $"{orderId}{statusCode}{grossAmount}{_options.ServerKey}";
        var expectedHex = Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
        var actualHex = signatureKey.Trim().ToLowerInvariant();

        var expectedBytes = Encoding.ASCII.GetBytes(expectedHex);
        var actualBytes = Encoding.ASCII.GetBytes(actualHex);
        return expectedBytes.Length == actualBytes.Length
            && CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    public async Task<MidtransCheckoutResult> CreateSnapCheckoutAsync(
        Account account,
        string? planCode,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(planCode) || !PlanCodes.Catalog.TryGetValue(planCode.Trim(), out var plan))
        {
            return new MidtransCheckoutResult(false, ErrorMessage: "Paket tidak dikenal.");
        }

        if (string.IsNullOrWhiteSpace(_options.ServerKey))
        {
            return new MidtransCheckoutResult(false, ErrorMessage: "Midtrans ServerKey belum dikonfigurasi.");
        }

        var now = timeProvider.GetUtcNow();
        var orderId = $"SW-{now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";

        var snapBody = new Dictionary<string, object?>
        {
            ["transaction_details"] = new
            {
                order_id = orderId,
                gross_amount = plan.PriceIdr,
            },
            ["item_details"] = new[]
            {
                new
                {
                    id = plan.Code,
                    price = plan.PriceIdr,
                    quantity = 1,
                    name = plan.DisplayName,
                },
            },
            ["customer_details"] = new
            {
                email = account.EmailNormalized,
            },
            ["expiry"] = new
            {
                unit = "hour",
                duration = 24,
            },
        };

        if (!string.IsNullOrWhiteSpace(_options.FinishRedirectUrl))
        {
            snapBody["callbacks"] = new { finish = _options.FinishRedirectUrl };
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_options.SnapBaseUrl}/snap/v1/transactions");
        var basicAuth = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_options.ServerKey}:"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Content = new StringContent(JsonSerializer.Serialize(snapBody), Encoding.UTF8, "application/json");

        using var response = await httpClient.SendAsync(request, ct);
        var responseJson = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Midtrans Snap error ({Status}): {Body}", (int)response.StatusCode, responseJson);
            return new MidtransCheckoutResult(false, ErrorMessage: "Gagal membuat transaksi Midtrans Snap.");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var token = doc.RootElement.TryGetProperty("token", out var tokProp) ? tokProp.GetString() : null;
        var redirectUrl = doc.RootElement.TryGetProperty("redirect_url", out var urlProp) ? urlProp.GetString() : null;
        if (string.IsNullOrWhiteSpace(redirectUrl))
        {
            return new MidtransCheckoutResult(false, ErrorMessage: "Respon Midtrans Snap tidak memuat redirect_url.");
        }

        db.PaymentOrders.Add(new PaymentOrder
        {
            OrderId = orderId,
            AccountId = account.Id,
            PlanCode = plan.Code,
            GrossAmountIdr = plan.PriceIdr,
            Status = "pending",
            SnapRedirectUrl = redirectUrl,
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync(ct);

        return new MidtransCheckoutResult(true, orderId, token, redirectUrl);
    }

    public async Task<MidtransWebhookResult> ProcessWebhookAsync(MidtransWebhookPayload payload, CancellationToken ct)
    {
        if (payload is null ||
            !VerifyWebhookSignature(payload.OrderId, payload.StatusCode, payload.GrossAmount, payload.SignatureKey))
        {
            logger.LogWarning("Rejected Midtrans webhook with invalid signature for order {OrderId}", payload?.OrderId);
            return new MidtransWebhookResult(false, StatusCodes.Status401Unauthorized, "Invalid signature");
        }

        if (!string.IsNullOrWhiteSpace(payload.Currency) &&
            !string.Equals(payload.Currency.Trim(), "IDR", StringComparison.OrdinalIgnoreCase))
        {
            return new MidtransWebhookResult(false, StatusCodes.Status400BadRequest, "Unsupported currency");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var order = await db.PaymentOrders.FirstOrDefaultAsync(x => x.OrderId == payload.OrderId, ct);
        if (order is null)
        {
            return new MidtransWebhookResult(false, StatusCodes.Status404NotFound, "Order not found");
        }

        if (!decimal.TryParse(
                payload.GrossAmount,
                NumberStyles.AllowLeadingWhite | NumberStyles.AllowTrailingWhite | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out var paidAmount)
            || paidAmount != order.GrossAmountIdr)
        {
            logger.LogWarning(
                "Rejected Midtrans webhook amount mismatch for {OrderId}: expected {Expected}, got {Actual}",
                order.OrderId,
                order.GrossAmountIdr,
                payload.GrossAmount);
            return new MidtransWebhookResult(false, StatusCodes.Status400BadRequest, "Amount mismatch");
        }

        var txStatus = (payload.TransactionStatus ?? string.Empty).Trim().ToLowerInvariant();
        var fraudStatus = payload.FraudStatus?.Trim().ToLowerInvariant();

        // Handle refund / chargeback by revoking the associated license (S1-04).
        if (txStatus is "refund" or "partial_refund" or "chargeback")
        {
            var licenseToRevoke = await db.Licenses.FirstOrDefaultAsync(x => x.ExternalOrderId == order.OrderId, ct);
            if (licenseToRevoke is not null)
            {
                licenseToRevoke.Status = "revoked";
            }

            order.Status = txStatus;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new MidtransWebhookResult(true, StatusCodes.Status200OK, "License revoked due to refund/chargeback");
        }

        var isPaid = txStatus == "settlement"
            || (txStatus == "capture" && (string.IsNullOrEmpty(fraudStatus) || fraudStatus == "accept"));

        if (!isPaid)
        {
            // Never regress an already-paid order back to expire/cancel/deny on out-of-order webhook delivery (S1-04).
            if (txStatus is "expire" or "cancel" or "deny" &&
                string.Equals(order.Status, "pending", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = txStatus;
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
            }

            return new MidtransWebhookResult(true, StatusCodes.Status200OK, $"Recorded status: {txStatus}");
        }

        // Idempotency guard: if license for this Midtrans order already exists, acknowledge cleanly.
        var alreadyIssued = await db.Licenses.AnyAsync(x => x.ExternalOrderId == order.OrderId, ct);
        if (alreadyIssued)
        {
            return new MidtransWebhookResult(true, StatusCodes.Status200OK, "Already processed");
        }

        if (!PlanCodes.Catalog.TryGetValue(order.PlanCode, out var plan))
        {
            return new MidtransWebhookResult(false, StatusCodes.Status400BadRequest, "Unknown plan");
        }

        var now = timeProvider.GetUtcNow();
        DateTimeOffset startsAt = now;
        DateTimeOffset? expiresAt = null;
        DateTimeOffset? updatesUntil = null;

        if (plan.DurationDays is int durationDays)
        {
            var activeExpiries = await db.Licenses
                .Where(l => l.AccountId == order.AccountId && l.Status == "active" && l.ExpiresAtUtc != null)
                .Select(l => l.ExpiresAtUtc)
                .ToListAsync(ct);

            var latestActiveExpiry = activeExpiries
                .Where(e => e > now)
                .OrderByDescending(e => e)
                .FirstOrDefault();

            startsAt = latestActiveExpiry ?? now;
            expiresAt = startsAt.AddDays(durationDays);
        }
        else if (plan.UpdatesMonths is int updatesMonths)
        {
            updatesUntil = now.AddMonths(updatesMonths);
        }

        db.Licenses.Add(new LicenseRecord
        {
            AccountId = order.AccountId,
            PlanCode = plan.Code,
            Status = "active",
            StartsAtUtc = startsAt,
            ExpiresAtUtc = expiresAt,
            UpdatesUntilUtc = updatesUntil,
            MaxDevices = plan.MaxDevices,
            PaymentProvider = "midtrans",
            ExternalOrderId = order.OrderId,
            GrossAmountIdr = order.GrossAmountIdr,
            CreatedAtUtc = now,
        });

        order.Status = "paid";
        order.PaidAtUtc = now;

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return new MidtransWebhookResult(true, StatusCodes.Status200OK, "Already processed");
        }

        logger.LogInformation("Activated {PlanCode} license for account {AccountId} via order {OrderId}", plan.Code, order.AccountId, order.OrderId);
        return new MidtransWebhookResult(true, StatusCodes.Status200OK, "License activated");
    }
}
