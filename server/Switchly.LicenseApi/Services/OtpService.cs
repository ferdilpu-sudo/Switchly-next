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

    /// <summary>
    /// Canonicalizes email aliases (+tags on all domains, dots and googlemail.com on Gmail)
    /// to prevent claiming multiple 7-day trials with the same inbox (#5).
    /// </summary>
    public static string CanonicalizeForTrialCheck(string normalizedEmail)
    {
        var at = normalizedEmail.LastIndexOf('@');
        if (at <= 0 || at == normalizedEmail.Length - 1)
        {
            return normalizedEmail;
        }

        var local = normalizedEmail[..at];
        var domain = normalizedEmail[(at + 1)..];

        if (string.Equals(domain, "googlemail.com", StringComparison.OrdinalIgnoreCase))
        {
            domain = "gmail.com";
        }

        var plusIdx = local.IndexOf('+');
        if (plusIdx > 0)
        {
            local = local[..plusIdx];
        }

        if (string.Equals(domain, "gmail.com", StringComparison.OrdinalIgnoreCase))
        {
            local = local.Replace(".", string.Empty, StringComparison.Ordinal);
        }

        return $"{local}@{domain}";
    }

    public async Task<OtpRequestResult> RequestOtpAsync(string? rawEmail, CancellationToken ct)
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
        var existingChallenges = await db.OtpChallenges
            .Where(x => x.EmailNormalized == email)
            .ToListAsync(ct);

        var recentCount = existingChallenges.Count(x => x.CreatedAtUtc >= windowStart);
        if (recentCount >= 3)
        {
            return new OtpRequestResult(false, "Terlalu banyak permintaan OTP. Coba lagi dalam 15 menit.");
        }

        var otpCode = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

        // Send email BEFORE committing the challenge so transient Resend errors never lock out the user (S1-07).
        try
        {
            await emailSender.SendOtpAsync(email, otpCode, _options.OtpExpiryMinutes, ct);
        }
        catch (Exception)
        {
            return new OtpRequestResult(false, "Gagal mengirim email OTP saat ini. Silakan coba beberapa saat lagi.");
        }

        // Invalidate any prior unconsumed OTPs for this email before issuing the new challenge.
        foreach (var old in existingChallenges.Where(x => x.ConsumedAtUtc == null))
        {
            old.ConsumedAtUtc = now;
        }

        db.OtpChallenges.Add(new OtpChallenge
        {
            EmailNormalized = email,
            CodeHash = HashSecret($"{email}:{otpCode}", _options.TokenHashPepper),
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddMinutes(_options.OtpExpiryMinutes),
            Attempts = 0,
        });

        await db.SaveChangesAsync(ct);
        return new OtpRequestResult(true);
    }

    public async Task<OtpVerifyResult> VerifyOtpAsync(
        string? rawEmail,
        string? code,
        string? deviceHash,
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

        var cleanDevice = deviceHash?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(cleanDevice) || cleanDevice.Length > 128)
        {
            return new OtpVerifyResult(false, ErrorMessage: "Identitas perangkat tidak valid.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

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

        // Atomically claim one attempt in SQL BEFORE comparing the hash to defeat ReadCommitted parallel brute-force (#1).
        var attemptUpdated = await db.OtpChallenges
            .Where(x => x.Id == latestChallenge.Id && x.ConsumedAtUtc == null && x.Attempts < _options.OtpMaxAttempts)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);

        if (attemptUpdated == 0)
        {
            return new OtpVerifyResult(false, ErrorMessage: "Batas percobaan OTP terlampaui atau kode sudah dipakai. Minta kode baru.");
        }

        var expectedHash = HashSecret($"{email}:{code.Trim()}", _options.TokenHashPepper);
        var actualBytes = Encoding.UTF8.GetBytes(latestChallenge.CodeHash);
        var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);
        if (!CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes))
        {
            await tx.CommitAsync(ct);
            return new OtpVerifyResult(false, ErrorMessage: "Kode OTP salah.");
        }

        // Atomically mark challenge consumed so parallel replay of a valid OTP can succeed at most once (#1).
        var consumed = await db.OtpChallenges
            .Where(x => x.Id == latestChallenge.Id && x.ConsumedAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ConsumedAtUtc, now), ct);

        if (consumed == 0)
        {
            await tx.RollbackAsync(ct);
            return new OtpVerifyResult(false, ErrorMessage: "Kode OTP sudah digunakan.");
        }

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
            DeviceHash = cleanDevice,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddDays(_options.SessionExpiryDays),
        };
        db.Sessions.Add(session);

        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new OtpVerifyResult(true, account, rawSessionToken);
        }
        catch (DbUpdateException)
        {
            await tx.RollbackAsync(ct);
            return new OtpVerifyResult(false, ErrorMessage: "Permintaan verifikasi sedang diproses secara bersamaan. Silakan coba lagi.");
        }
    }

    public async Task<SessionRecord?> ValidateSessionAsync(string? rawSessionToken, CancellationToken ct)
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

        return session is not null
            && session.ExpiresAtUtc > now
            && string.Equals(session.Account.Status, "active", StringComparison.OrdinalIgnoreCase)
            ? session
            : null;
    }

    public static string HashSecret(string input, string pepper)
    {
        var keyBytes = Encoding.UTF8.GetBytes(pepper ?? string.Empty);
        var dataBytes = Encoding.UTF8.GetBytes(input);
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, dataBytes)).ToLowerInvariant();
    }
}
