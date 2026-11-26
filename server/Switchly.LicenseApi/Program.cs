using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
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

builder.Services.Configure<LicenseApiOptions>(builder.Configuration.GetSection(LicenseApiOptions.SectionName));
builder.Services.Configure<MidtransOptions>(builder.Configuration.GetSection(MidtransOptions.SectionName));
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection(EmailOptions.SectionName));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<IEd25519TokenService, Ed25519TokenService>();

var connectionString = builder.Configuration.GetConnectionString("Default")
    ?? throw new InvalidOperationException("ConnectionStrings:Default is required.");

builder.Services.AddDbContext<LicenseDbContext>(opt => opt.UseNpgsql(connectionString));

var emailProvider = builder.Configuration.GetValue<string>("Email:Provider") ?? "Console";
if (string.Equals(emailProvider, "Resend", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddHttpClient<IEmailSender, ResendEmailSender>();
}
else
{
    builder.Services.AddScoped<IEmailSender, ConsoleEmailSender>();
}

builder.Services.AddHttpClient<IMidtransService, MidtransService>();
builder.Services.AddScoped<OtpService>();
builder.Services.AddScoped<EntitlementIssuer>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    await db.Database.EnsureCreatedAsync();
}

app.MapLicenseApiEndpoints();

await app.RunAsync();
