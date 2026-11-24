using Microsoft.EntityFrameworkCore;
using Switchly.Domain.Groups;
using Switchly.Domain.Profiles;

namespace Switchly.Infrastructure.Persistence;

/// <summary>Application metadata store (ADR-004): plain SQLite via EF Core.</summary>
public sealed class SwitchlyDbContext : DbContext
{
    public SwitchlyDbContext(DbContextOptions<SwitchlyDbContext> options)
        : base(options)
    {
    }

    public DbSet<BrowserProfile> Profiles => Set<BrowserProfile>();

    public DbSet<ProfileGroup> Groups => Set<ProfileGroup>();

    public DbSet<ProfileGroupMembership> GroupMemberships => Set<ProfileGroupMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BrowserProfile>(e =>
        {
            e.HasKey(p => p.ProfileId);
            e.Property(p => p.Name).IsRequired().HasMaxLength(80);
            e.Property(p => p.ColorHex).HasMaxLength(7);
            e.Property(p => p.IconKey).HasMaxLength(64);
            e.Property(p => p.StartupUrl).HasMaxLength(2048);
            e.Property(p => p.StorageRelativePath).IsRequired();
            e.HasIndex(p => p.StorageRelativePath).IsUnique();
            e.Property(p => p.IsFavorite).HasDefaultValue(false);
            e.Property(p => p.IsArchived).HasDefaultValue(false);
            e.Property(p => p.RestoreLastSession).HasDefaultValue(true);
            e.Property(p => p.PersistSessionCookies).HasDefaultValue(true);
        });

        modelBuilder.Entity<ProfileGroup>(e =>
        {
            e.HasKey(g => g.GroupId);
            e.Property(g => g.Name).IsRequired().HasMaxLength(80);
            e.Property(g => g.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(g => g.DomainKey).HasMaxLength(253);
            e.Property(g => g.IsHidden).HasDefaultValue(false);
            e.HasIndex(g => new { g.Kind, g.DomainKey })
                .IsUnique()
                .HasFilter("\"Kind\" = 'WebsiteDomain'");
        });

        modelBuilder.Entity<ProfileGroupMembership>(e =>
        {
            e.HasKey(m => new { m.ProfileId, m.GroupId });
            e.Property(m => m.MembershipSource).HasConversion<string>().HasMaxLength(16);

            e.HasOne<BrowserProfile>()
                .WithMany()
                .HasForeignKey(m => m.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<ProfileGroup>()
                .WithMany()
                .HasForeignKey(m => m.GroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
