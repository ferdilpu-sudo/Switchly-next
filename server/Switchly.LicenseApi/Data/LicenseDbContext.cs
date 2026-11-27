using Microsoft.EntityFrameworkCore;

namespace Switchly.LicenseApi.Data;

public sealed class LicenseDbContext(DbContextOptions<LicenseDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<OtpChallenge> OtpChallenges => Set<OtpChallenge>();
    public DbSet<SessionRecord> Sessions => Set<SessionRecord>();
    public DbSet<TrialDeviceClaim> TrialDeviceClaims => Set<TrialDeviceClaim>();
    public DbSet<LicenseRecord> Licenses => Set<LicenseRecord>();
    public DbSet<PaymentOrder> PaymentOrders => Set<PaymentOrder>();
    public DbSet<DeviceActivation> DeviceActivations => Set<DeviceActivation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.ToTable("accounts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EmailNormalized).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => x.EmailNormalized).IsUnique();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
        });

        modelBuilder.Entity<OtpChallenge>(entity =>
        {
            entity.ToTable("otp_challenges");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EmailNormalized).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => new { x.EmailNormalized, x.CreatedAtUtc });
            entity.Property(x => x.CodeHash).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<SessionRecord>(entity =>
        {
            entity.ToTable("sessions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.TokenHash).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.Property(x => x.DeviceHash).HasMaxLength(128).IsRequired();
            entity.HasOne(x => x.Account).WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TrialDeviceClaim>(entity =>
        {
            entity.ToTable("trial_device_claims");
            entity.HasKey(x => x.DeviceHash);
            entity.Property(x => x.DeviceHash).HasMaxLength(128).IsRequired();
        });

        modelBuilder.Entity<LicenseRecord>(entity =>
        {
            entity.ToTable("licenses");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.PaymentProvider).HasMaxLength(32).IsRequired();
            entity.Property(x => x.ExternalOrderId).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => x.ExternalOrderId).IsUnique();
            entity.HasOne(x => x.Account).WithMany(a => a.Licenses).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentOrder>(entity =>
        {
            entity.ToTable("payment_orders");
            entity.HasKey(x => x.OrderId);
            entity.Property(x => x.OrderId).HasMaxLength(128).IsRequired();
            entity.Property(x => x.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.HasIndex(x => x.AccountId);
        });

        modelBuilder.Entity<DeviceActivation>(entity =>
        {
            entity.ToTable("device_activations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.DeviceHash).HasMaxLength(128).IsRequired();
            entity.Property(x => x.DeviceLabel).HasMaxLength(128).IsRequired();
            entity.HasIndex(x => new { x.AccountId, x.DeviceHash }).IsUnique();
            entity.HasOne(x => x.Account).WithMany(a => a.Devices).HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
