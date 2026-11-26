using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Switchly.LicenseApi.Data;
using Switchly.LicenseApi.Options;

namespace Switchly.LicenseApi.Services;

public sealed record OtpRequestResult(bool Succeeded, string? ErrorMessage = null);

public sealed record OtpVerifyResult(
    bool Succeeded,
    Account? Account = null,
    string? SessionToken = null,
    string? ErrorMessage = null);

public sealed class OtpService(
    LicenseDbContext db,
    IEmailSender emailSender,
    IOptions<LicenseApiOptions> options,
    TimeProvider timeProvider)
{
    private static readonly HashSet<string> DisposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com", "guerrillamail.com", "10minutemail.com", "temp-mail.org",
        "tempmail.com", "yopmail.com", "throwawaymail.com", "sharklasers.com",
        "getnada.com", "trashmail.com", "maildrop.cc", "dispostable.com",
    };

    private readonly LicenseApiOptions _options = options.Value;

    public static bool TryNormalizeEmail(string? rawEmail, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(rawEmail))
        {
            return false;
        }

        var trimmed = rawEmail.Trim();
        if (trimmed.Length > 320)
        {
            return false;
        }

        try
        {
            var addr = new MailAddress(trimmed);
            if (!string.Equals(addr.Address, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            normalized = trimmed.ToLowerInvariant();
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static bool IsDisposableEmail(string normalizedEmail)
    {
        var at = normalizedEmail.LastIndexOf('@');
        if (at < 0 || at == normalizedEmail.Length - 1)
        {
            return true;
        }

        var domain = normalizedEmail[(at + 1)..];
        return DisposableDomains.Contains(domain);
    }

    public async Task<OtpRequestResult> RequestOtpAsync(string rawEmail, CancellationToken ct)
    {
        if (!TryNormalizeEmail(rawEmail, out var email))
        {
            return new OtpRequestResult(false, "Format alamat email tidak valid.");
        }

        if (IsDisposableEmail(email))
        {
            return new OtpRequestResult(false, "Gunakan alamat email permanen (email sementara tidak didukung).");
        }

        var now = timeProvider.GetUtcNow();
        var windowStart = now.AddMinutes(-15);
        var recentTimestamps = await db.OtpChallenges
            .Where(x => x.EmailNormalized == email)
            .Select(x => x.CreatedAtUtc)
            .ToListAsync(ct);

        var recentCount = recentTimestamps.Count(t => t >= windowStart);

        if (recentCount >= 3)
        {
            return new OtpRequestResult(false, "Terlalu banyak permintaan OTP. Coba lagi dalam 15 menit.");
        }

        var otpCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challenge = new OtpChallenge
        {
            EmailNormalized = email,
            CodeHash = HashSecret($"{email}:{otpCode}", _options.TokenHashPepper),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(_options.OtpExpiryMinutes),
            Attempts = 0,
        };

        db.OtpChallenges.Add(challenge);
        await db.SaveChangesAsync(ct);

        await emailSender.SendOtpAsync(email, otpCode, _options.OtpExpiryMinutes, ct);
        return new OtpRequestResult(true);
    }

    public async Task<OtpVerifyResult> VerifyOtpAsync(
        string rawEmail,
        string code,
        string deviceHash,
        CancellationToken ct)
    {
        if (!TryNormalizeEmail(rawEmail, out var email))
        {
            return new OtpVerifyResult(false, ErrorMessage: "Format email tidak valid.");
        }

        if (string.IsNullOrWhiteSpace(code) || code.Trim().Length != 6)
        {
            return new OtpVerifyResult(false, ErrorMessage: "Kode OTP harus 6 digit.");
        }

        if (string.IsNullOrWhiteSpace(deviceHash) || deviceHash.Length > 128)
        {
            return new OtpVerifyResult(false, ErrorMessage: "Identitas perangkat tidak valid.");
        }

        var now = timeProvider.GetUtcNow();
        var activeChallenges = await db.OtpChallenges
            .Where(x => x.EmailNormalized == email && x.ConsumedAtUtc == null)
            .ToListAsync(ct);

        var latestChallenge = activeChallenges
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefault();

        if (latestChallenge is null || latestChallenge.ExpiresAtUtc < now)
        {
            return new OtpVerifyResult(false, ErrorMessage: "Kode OTP sudah kedaluwarsa atau belum diminta.");
        }

        if (latestChallenge.Attempts >= _options.OtpMaxAttempts)
        {
            return new OtpVerifyResult(false, ErrorMessage: "Batas percobaan OTP terlampaui. Minta kode baru.");
        }

        var expectedHash = HashSecret($"{email}:{code.Trim()}", _options.TokenHashPepper);
        var actualBytes = Encoding.UTF8.GetBytes(latestChallenge.CodeHash);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        if (!CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
        {
            latestChallenge.Attempts++;
            await db.SaveChangesAsync(ct);
            return new OtpVerifyResult(false, ErrorMessage: "Kode OTP salah.");
        }

        latestChallenge.ConsumedAtUtc = now;

        var account = await db.Accounts
            .Include(a => a.Licenses)
            .Include(a => a.Devices)
            .FirstOrDefaultAsync(a => a.EmailNormalized == email, ct);

        if (account is null)
        {
            account = new Account
            {
                EmailNormalized = email,
                CreatedAtUtc = now,
                Status = "active",
            };
            db.Accounts.Add(account);
        }

        if (!string.Equals(account.Status, "active", StringComparison.OrdinalIgnoreCase))
        {
            return new OtpVerifyResult(false, ErrorMessage: "Akun sedang dinonaktifkan.");
        }

        var rawSessionToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        var session = new SessionRecord
        {
            AccountId = account.Id,
            TokenHash = HashSecret(rawSessionToken, _options.TokenHashPepper),
            DeviceHash = deviceHash.Trim(),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_options.SessionExpiryDays),
        };
        db.Sessions.Add(session);

        await db.SaveChangesAsync(ct);
        return new OtpVerifyResult(true, account, rawSessionToken);
    }

    public async Task<SessionRecord?> ValidateSessionAsync(string rawSessionToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(rawSessionToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var hash = HashSecret(rawSessionToken.Trim(), _options.TokenHashPepper);
        var session = await db.Sessions
            .Include(s => s.Account)
                .ThenInclude(a => a.Licenses)
            .Include(s => s.Account)
                .ThenInclude(a => a.Devices)
            .FirstOrDefaultAsync(
                s => s.TokenHash == hash && s.RevokedAtUtc == null,
                ct);

        return session is not null && session.ExpiresAtUtc > now ? session : null;
    }

    public static string HashSecret(string input, string pepper)
    {
        var bytes = Encoding.UTF8.GetBytes($"{pepper}:{input}");
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
