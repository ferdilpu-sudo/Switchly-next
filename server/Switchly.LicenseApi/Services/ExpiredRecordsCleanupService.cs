using Microsoft.EntityFrameworkCore;
using Switchly.LicenseApi.Data;

namespace Switchly.LicenseApi.Services;

/// <summary>
/// Prunes expired OTP challenges, expired/revoked sessions, and abandoned pending payment orders daily (S1-09).
/// </summary>
public sealed class ExpiredRecordsCleanupService(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<ExpiredRecordsCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
                var now = timeProvider.GetUtcNow();
                var otpCutoff = now.AddDays(-1);
                var sessionCutoff = now.AddDays(-7);
                var orderCutoff = now.AddDays(-7);

                var staleOtps = (await db.OtpChallenges.ToListAsync(stoppingToken))
                    .Where(x => x.ExpiresAtUtc < otpCutoff)
                    .ToList();
                var staleSessions = (await db.Sessions.ToListAsync(stoppingToken))
                    .Where(x => x.ExpiresAtUtc < now || (x.RevokedAtUtc is not null && x.RevokedAtUtc < sessionCutoff))
                    .ToList();
                var staleOrders = (await db.PaymentOrders.Where(o => o.Status != "paid").ToListAsync(stoppingToken))
                    .Where(o => o.CreatedAtUtc < orderCutoff)
                    .ToList();

                if (staleOtps.Count > 0 || staleSessions.Count > 0 || staleOrders.Count > 0)
                {
                    db.OtpChallenges.RemoveRange(staleOtps);
                    db.Sessions.RemoveRange(staleSessions);
                    db.PaymentOrders.RemoveRange(staleOrders);
                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Periodic cleanup of expired license API records encountered an error.");
            }
        }
    }
}
