using Microsoft.EntityFrameworkCore;

namespace Switchly.LicenseApi.Data;

/// <summary>
/// Version-tracked PostgreSQL schema migrator (S1-06).
/// Replaces one-shot EnsureCreatedAsync() in production so future schema v2/v3 migrations
/// execute deterministically inside transactions while keeping SQLite in-memory tests fast.
/// </summary>
public static class SchemaMigrator
{
    public static async Task ApplyAsync(LicenseDbContext db, CancellationToken ct = default)
    {
        var provider = db.Database.ProviderName ?? string.Empty;
        if (!provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
        {
            await db.Database.EnsureCreatedAsync(ct);
            return;
        }

        await db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS schema_migrations (
                version INTEGER PRIMARY KEY,
                name VARCHAR(128) NOT NULL,
                applied_at_utc TIMESTAMPTZ NOT NULL
            );
            """, ct);

        var appliedVersions = await db.Database
            .SqlQueryRaw<int>("SELECT version AS \"Value\" FROM schema_migrations")
            .ToListAsync(ct);

        if (!appliedVersions.Contains(1))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Database.ExecuteSqlRawAsync("""
                CREATE TABLE IF NOT EXISTS accounts (
                    "Id" UUID PRIMARY KEY,
                    "EmailNormalized" VARCHAR(320) NOT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL,
                    "TrialClaimedAtUtc" TIMESTAMPTZ NULL,
                    "TrialExpiresAtUtc" TIMESTAMPTZ NULL,
                    "Status" VARCHAR(32) NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_accounts_EmailNormalized" ON accounts ("EmailNormalized");

                CREATE TABLE IF NOT EXISTS otp_challenges (
                    "Id" UUID PRIMARY KEY,
                    "EmailNormalized" VARCHAR(320) NOT NULL,
                    "CodeHash" VARCHAR(128) NOT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL,
                    "ExpiresAtUtc" TIMESTAMPTZ NOT NULL,
                    "Attempts" INTEGER NOT NULL,
                    "ConsumedAtUtc" TIMESTAMPTZ NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_otp_challenges_EmailNormalized_CreatedAtUtc"
                    ON otp_challenges ("EmailNormalized", "CreatedAtUtc");

                CREATE TABLE IF NOT EXISTS sessions (
                    "Id" UUID PRIMARY KEY,
                    "AccountId" UUID NOT NULL REFERENCES accounts("Id") ON DELETE CASCADE,
                    "TokenHash" VARCHAR(128) NOT NULL,
                    "DeviceHash" VARCHAR(128) NOT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL,
                    "ExpiresAtUtc" TIMESTAMPTZ NOT NULL,
                    "RevokedAtUtc" TIMESTAMPTZ NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_sessions_TokenHash" ON sessions ("TokenHash");

                CREATE TABLE IF NOT EXISTS trial_device_claims (
                    "DeviceHash" VARCHAR(128) PRIMARY KEY,
                    "AccountId" UUID NOT NULL,
                    "ClaimedAtUtc" TIMESTAMPTZ NOT NULL
                );

                CREATE TABLE IF NOT EXISTS licenses (
                    "Id" UUID PRIMARY KEY,
                    "AccountId" UUID NOT NULL REFERENCES accounts("Id") ON DELETE CASCADE,
                    "PlanCode" VARCHAR(64) NOT NULL,
                    "Status" VARCHAR(32) NOT NULL,
                    "StartsAtUtc" TIMESTAMPTZ NOT NULL,
                    "ExpiresAtUtc" TIMESTAMPTZ NULL,
                    "UpdatesUntilUtc" TIMESTAMPTZ NULL,
                    "MaxDevices" INTEGER NOT NULL,
                    "PaymentProvider" VARCHAR(32) NOT NULL,
                    "ExternalOrderId" VARCHAR(128) NOT NULL,
                    "GrossAmountIdr" BIGINT NOT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_licenses_ExternalOrderId" ON licenses ("ExternalOrderId");

                CREATE TABLE IF NOT EXISTS payment_orders (
                    "OrderId" VARCHAR(128) PRIMARY KEY,
                    "AccountId" UUID NOT NULL,
                    "PlanCode" VARCHAR(64) NOT NULL,
                    "GrossAmountIdr" BIGINT NOT NULL,
                    "Status" VARCHAR(32) NOT NULL,
                    "SnapRedirectUrl" TEXT NULL,
                    "CreatedAtUtc" TIMESTAMPTZ NOT NULL,
                    "PaidAtUtc" TIMESTAMPTZ NULL
                );
                CREATE INDEX IF NOT EXISTS "IX_payment_orders_AccountId" ON payment_orders ("AccountId");

                CREATE TABLE IF NOT EXISTS device_activations (
                    "Id" UUID PRIMARY KEY,
                    "AccountId" UUID NOT NULL REFERENCES accounts("Id") ON DELETE CASCADE,
                    "DeviceHash" VARCHAR(128) NOT NULL,
                    "DeviceLabel" VARCHAR(128) NOT NULL,
                    "ActivatedAtUtc" TIMESTAMPTZ NOT NULL,
                    "LastSeenUtc" TIMESTAMPTZ NOT NULL,
                    "RevokedAtUtc" TIMESTAMPTZ NULL
                );
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_device_activations_AccountId_DeviceHash"
                    ON device_activations ("AccountId", "DeviceHash");

                INSERT INTO schema_migrations (version, name, applied_at_utc)
                VALUES (1, 'v1_initial_schema', NOW());
                """, ct);
            await tx.CommitAsync(ct);
        }
    }
}
