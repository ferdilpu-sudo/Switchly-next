using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Switchly.LicenseApi.Crypto;
using Switchly.LicenseApi.Data;
using Switchly.LicenseApi.Options;
using Switchly.LicenseApi.Services;
using Xunit;

namespace Switchly.LicenseApi.Tests;

public sealed class LicenseApiTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly LicenseDbContext _db;
    private readonly Ed25519TokenService _tokenService;
    private readonly IOptions<LicenseApiOptions> _apiOptions;
    private readonly IOptions<MidtransOptions> _midtransOptions;

    public LicenseApiTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var dbOptions = new DbContextOptionsBuilder<LicenseDbContext>()
            .UseSqlite(_connection)
            .Options;

        _db = new LicenseDbContext(dbOptions);
        _db.Database.EnsureCreated();

        var (priv, _) = Ed25519TokenService.GenerateKeyPair();
        _apiOptions = Microsoft.Extensions.Options.Options.Create(new LicenseApiOptions
        {
            Ed25519PrivateKeyBase64 = priv,
            TokenHashPepper = "test-pepper-12345",
            FreeMaxProfiles = 5,
            TrialDays = 7,
            DefaultMaxDevices = 2,
            OfflineGraceDays = 14,
            TokenRefreshDays = 7,
        });

        _midtransOptions = Microsoft.Extensions.Options.Options.Create(new MidtransOptions
        {
            IsProduction = false,
            ServerKey = "SB-Mid-server-TESTKEY",
        });

        _tokenService = new Ed25519TokenService(_apiOptions);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public void Ed25519Token_SignAndVerify_RoundTripsAndRejectsTampering()
    {
        var now = new DateTimeOffset(2026, 11, 26, 5, 0, 0, TimeSpan.Zero);
        var payload = new LicenseTokenPayload(
            Version: 1,
            AccountId: "acct123",
            Tier: "pro_perpetual",
            MaxProfiles: null,
            DeviceHash: "dev-hash-1",
            IssuedAtUtc: now,
            ExpiresAtUtc: null,
            UpdatesUntilUtc: now.AddYears(1),
            RefreshAfterUtc: now.AddDays(7),
            OfflineGraceUntilUtc: now.AddYears(100));

        var signed = _tokenService.Sign(payload);

        Assert.True(_tokenService.TryVerify(signed, out var verified));
        Assert.NotNull(verified);
        Assert.Equal("pro_perpetual", verified!.Tier);
        Assert.Equal("dev-hash-1", verified.DeviceHash);

        // Tamper one character in the payload segment -> signature MUST fail
        var tampered = (signed[0] == 'A' ? 'B' : 'A') + signed[1..];
        Assert.False(_tokenService.TryVerify(tampered, out _));
    }

    [Fact]
    public async Task OtpService_BlocksDisposableEmail_AndVerifiesValidCode()
    {
        var sender = new CapturingEmailSender();
        var otp = new OtpService(_db, sender, _apiOptions, TimeProvider.System);

        var blocked = await otp.RequestOtpAsync("abuse@mailinator.com", CancellationToken.None);
        Assert.False(blocked.Succeeded);

        var ok = await otp.RequestOtpAsync("User@Example.com", CancellationToken.None);
        Assert.True(ok.Succeeded);
        Assert.Equal("user@example.com", sender.LastEmail);
        Assert.Equal(6, sender.LastCode!.Length);

        var badCode = await otp.VerifyOtpAsync("user@example.com", "000000", "dev-1", CancellationToken.None);
        Assert.False(badCode.Succeeded);

        var verified = await otp.VerifyOtpAsync("user@example.com", sender.LastCode!, "dev-1", CancellationToken.None);
        Assert.True(verified.Succeeded);
        Assert.NotNull(verified.Account);
        Assert.False(string.IsNullOrWhiteSpace(verified.SessionToken));
    }

    [Fact]
    public async Task TrialStart_EnforcesOneTrialPerAccountAndOneTrialPerDevice()
    {
        var issuer = new EntitlementIssuer(_db, _tokenService, _apiOptions, TimeProvider.System);

        var acct1 = new Account { EmailNormalized = "one@example.com" };
        var acct2 = new Account { EmailNormalized = "two@example.com" };
        _db.Accounts.AddRange(acct1, acct2);
        await _db.SaveChangesAsync();

        // First claim succeeds and issues signed trial_pro token
        var first = await issuer.StartTrialAsync(acct1, "device-hash-A", "Laptop-1", CancellationToken.None);
        Assert.True(first.Succeeded);
        Assert.True(_tokenService.TryVerify(first.EntitlementToken!, out var tokenPayload));
        Assert.Equal("trial_pro", tokenPayload!.Tier);
        Assert.Null(tokenPayload.MaxProfiles);

        // Same account on another device is rejected
        var secondSameAccount = await issuer.StartTrialAsync(acct1, "device-hash-B", "Laptop-2", CancellationToken.None);
        Assert.False(secondSameAccount.Succeeded);

        // Different account on the SAME device is rejected (anti-abuse)
        var secondSameDevice = await issuer.StartTrialAsync(acct2, "device-hash-A", "Laptop-1", CancellationToken.None);
        Assert.False(secondSameDevice.Succeeded);
    }

    [Fact]
    public async Task MidtransWebhook_VerifiesSha512_ActivatesLicenseIdempotently_AndRejectsAmountTampering()
    {
        var acct = new Account { EmailNormalized = "buyer@example.com" };
        _db.Accounts.Add(acct);
        _db.PaymentOrders.Add(new PaymentOrder
        {
            OrderId = "SW-ORDER-001",
            AccountId = acct.Id,
            PlanCode = PlanCodes.ProAnnual,
            GrossAmountIdr = 249_000,
            Status = "pending",
        });
        await _db.SaveChangesAsync();

        var midtrans = new MidtransService(
            new HttpClient(),
            _db,
            _midtransOptions,
            TimeProvider.System,
            NullLogger<MidtransService>.Instance);

        // 1. Invalid signature -> 401
        var badSig = await midtrans.ProcessWebhookAsync(
            new MidtransWebhookPayload("SW-ORDER-001", "200", "249000.00", "deadbeef", "settlement", "accept"),
            CancellationToken.None);
        Assert.Equal(401, badSig.HttpStatusCode);

        // 2. Tampered gross_amount (with valid signature for tampered amount) -> 400
        var tamperedSig = ComputeMidtransSig("SW-ORDER-001", "200", "1000.00", "SB-Mid-server-TESTKEY");
        var badAmount = await midtrans.ProcessWebhookAsync(
            new MidtransWebhookPayload("SW-ORDER-001", "200", "1000.00", tamperedSig, "settlement", "accept"),
            CancellationToken.None);
        Assert.Equal(400, badAmount.HttpStatusCode);

        // 3. Valid settlement -> 200 and license created
        var validSig = ComputeMidtransSig("SW-ORDER-001", "200", "249000.00", "SB-Mid-server-TESTKEY");
        var paid = await midtrans.ProcessWebhookAsync(
            new MidtransWebhookPayload("SW-ORDER-001", "200", "249000.00", validSig, "settlement", "accept"),
            CancellationToken.None);
        Assert.Equal(200, paid.HttpStatusCode);
        Assert.Equal(1, await _db.Licenses.CountAsync());

        // 4. Duplicate webhook -> 200 idempotent, still 1 license
        var duplicate = await midtrans.ProcessWebhookAsync(
            new MidtransWebhookPayload("SW-ORDER-001", "200", "249000.00", validSig, "settlement", "accept"),
            CancellationToken.None);
        Assert.Equal(200, duplicate.HttpStatusCode);
        Assert.Equal(1, await _db.Licenses.CountAsync());

        // 5. Issued token reflects pro_subscription & enforces max 2 devices
        var issuer = new EntitlementIssuer(_db, _tokenService, _apiOptions, TimeProvider.System);
        var dev1 = await issuer.IssueForDeviceAsync(acct, "pc-1", "PC 1", CancellationToken.None);
        var dev2 = await issuer.IssueForDeviceAsync(acct, "pc-2", "PC 2", CancellationToken.None);
        var dev3 = await issuer.IssueForDeviceAsync(acct, "pc-3", "PC 3", CancellationToken.None);

        Assert.True(dev1.Succeeded);
        Assert.Equal("pro_subscription", dev1.Payload!.Tier);
        Assert.True(dev2.Succeeded);
        Assert.False(dev3.Succeeded);
    }

    private static string ComputeMidtransSig(string orderId, string statusCode, string grossAmount, string serverKey)
    {
        var raw = $"{orderId}{statusCode}{grossAmount}{serverKey}";
        return Convert.ToHexString(SHA512.HashData(Encoding.UTF8.GetBytes(raw))).ToLowerInvariant();
    }

    private sealed class CapturingEmailSender : IEmailSender
    {
        public string? LastEmail { get; private set; }
        public string? LastCode { get; private set; }

        public Task SendOtpAsync(string toEmail, string otpCode, int expiryMinutes, CancellationToken ct)
        {
            LastEmail = toEmail;
            LastCode = otpCode;
            return Task.CompletedTask;
        }
    }
}
