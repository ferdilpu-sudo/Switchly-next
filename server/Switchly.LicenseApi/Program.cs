using System.Net;
using System.Security.Cryptography;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Switchly.LicenseApi.Crypto;
using Switchly.LicenseApi.Data;
using Switchly.LicenseApi.Endpoints;
using Switchly.LicenseApi.Options;
using Switchly.LicenseApi.Services;

if (args.Contains("--generate-keys", StringComparer.OrdinalIgnoreCase))
{
    var (priv, pub) = Ed25519TokenService.GenerateKeyPair();
    var pepper = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
    Console.WriteLine("# Copy into server/.env (NEVER commit private key or pepper to Git):");
    Console.WriteLine($"LICENSE_ED25519_PRIVATE_KEY={priv}");
    Console.WriteLine($"LICENSE_TOKEN_HASH_PEPPER={pepper}");
    Console.WriteLine();
    Console.WriteLine("# Embed this Public Key in Switchly Desktop:");
    Console.WriteLine($"SWITCHLY_ED25519_PUBLIC_KEY={pub}");
    return;
}

var builder = WebApplication.CreateBuilder(args);

// Cap request payloads to 64 KB — all valid OTP/Checkout/Midtrans webhook payloads are < 4 KB (S1-09).
builder.WebHost.ConfigureKestrel(kestrel => kestrel.Limits.MaxRequestBodySize = 64 * 1024);

builder.Services.AddOptions<LicenseApiOptions>()
    .Bind(builder.Configuration.GetSection(LicenseApiOptions.SectionName))
    .Validate(o => !string.IsNullOrWhiteSpace(o.Ed25519PrivateKeyBase64), "LicenseApi:Ed25519PrivateKeyBase64 is required.")
    .Validate(o => !string.IsNullOrWhiteSpace(o.TokenHashPepper) && o.TokenHashPepper.Trim().Length >= 32, "LicenseApi:TokenHashPepper must be at least 32 characters.")
    .ValidateOnStart();

builder.Services.AddOptions<MidtransOptions>()
    .Bind(builder.Configuration.GetSection(MidtransOptions.SectionName));

builder.Services.AddOptions<EmailOptions>()
    .Bind(builder.Configuration.GetSection(EmailOptions.SectionName))
    .Validate(
        o => !builder.Environment.IsProduction() || !string.Equals(o.Provider, "Console", StringComparison.OrdinalIgnoreCase),
        "Email:Provider=Console is forbidden in Production; configure Resend.")
    .Validate(
        o => !string.Equals(o.Provider, "Resend", StringComparison.OrdinalIgnoreCase) || !string.IsNullOrWhiteSpace(o.ResendApiKey),
        "Email:ResendApiKey is required when Email:Provider=Resend.")
    .ValidateOnStart();

// Trust X-Forwarded-For / X-Forwarded-Proto from local Caddy reverse proxy on Docker private bridge networks (S1-01).
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("10.0.0.0"), 8));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("172.16.0.0"), 12));
    options.KnownIPNetworks.Add(new System.Net.IPNetwork(IPAddress.Parse("192.168.0.0"), 16));
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);
});

// Per-IP + Global rate limiting to protect Resend email quota and OTP brute-force resistance (S1-01).
builder.Services.AddRateLimiter(limiter =>
{
    limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    // Global safety ceiling across all endpoints (protects OCI VM & outbound Resend quota).
    limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
    {
        var path = ctx.Request.Path.Value ?? string.Empty;
        if (path.EndsWith("/auth/otp/request", StringComparison.OrdinalIgnoreCase))
        {
            return RateLimitPartition.GetTokenBucketLimiter("global-otp-email", _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 60,
                TokensPerPeriod = 60,
                ReplenishmentPeriod = TimeSpan.FromHours(1),
                AutoReplenishment = true,
                QueueLimit = 0,
            });
        }

        return RateLimitPartition.GetNoLimiter("default");
    });

    limiter.AddPolicy(ApiEndpoints.OtpRequestRateLimitPolicy, ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"otp-req:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
        });
    });

    limiter.AddPolicy(ApiEndpoints.OtpVerifyRateLimitPolicy, ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"verify:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 12,
            Window = TimeSpan.FromMinutes(15),
            QueueLimit = 0,
        });
    });

    limiter.AddPolicy(ApiEndpoints.CheckoutRateLimitPolicy, ctx =>
    {
        var ip = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter($"checkout:{ip}", _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 15,
            Window = TimeSpan.FromHours(1),
            QueueLimit = 0,
        });
    });
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEd25519TokenService, Ed25519TokenService>();

var connectionString = ResolvePostgresConnectionString(builder.Configuration);
builder.Services.AddDbContext<LicenseDbContext>(opt => opt.UseNpgsql(connectionString));

var emailProvider = builder.Configuration.GetValue<string>("Email:Provider") ?? "Console";
if (string.Equals(emailProvider, "Resend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>(c => c.Timeout = TimeSpan.FromSeconds(10));
}
else
{
    builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
}

builder.Services.AddHttpClient<IMidtransService, MidtransService>(c => c.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<EntitlementIssuer>();
builder.Services.AddHostedService<ExpiredRecordsCleanupService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    await SchemaMigrator.ApplyAsync(db);
}

app.UseForwardedHeaders();
app.UseRateLimiter();
app.MapLicenseApiEndpoints();

await app.RunAsync();

static string ResolvePostgresConnectionString(IConfiguration config)
{
    var pgHost = Environment.GetEnvironmentVariable("POSTGRES_HOST");
    var pgPass = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD");
    if (!string.IsNullOrWhiteSpace(pgHost) && !string.IsNullOrWhiteSpace(pgPass))
    {
        // Build via NpgsqlConnectionStringBuilder so special characters in POSTGRES_PASSWORD never corrupt connection string (S1-10).
        var csb = new NpgsqlConnectionStringBuilder
        {
            Host = pgHost,
            Port = int.TryParse(Environment.GetEnvironmentVariable("POSTGRES_PORT"), out var p) ? p : 5432,
            Database = Environment.GetEnvironmentVariable("POSTGRES_DB") ?? "switchly_license",
            Username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "switchly",
            Password = pgPass,
        };
        return csb.ConnectionString;
    }

    var cs = config.GetConnectionString("Default");
    if (string.IsNullOrWhiteSpace(cs))
    {
        throw new InvalidOperationException("ConnectionStrings:Default or POSTGRES_HOST/POSTGRES_PASSWORD is required.");
    }

    return cs;
}
