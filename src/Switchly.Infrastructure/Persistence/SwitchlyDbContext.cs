using Microsoft.EntityFrameworkCore;
using Switchly.Domain.Groups;
using Switchly.Domain.Profiles;

namespace Switchly.Infrastructure.Persistence;

/// <summary>Application metadata store (ADR-004): SQLite via EF Core.</summary>
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
        });

        modelBuilder.Entity<ProfileGroup>(e =>
        {
            e.HasKey(g => g.GroupId);
            e.Property(g => g.Name).IsRequired().HasMaxLength(80);
            e.Property(g => g.Kind).HasConversion<string>().HasMaxLength(32);
            e.Property(g => g.DomainKey).HasMaxLength(253);
            e.HasIndex(g => new { g.Kind, g.DomainKey });
        });

        modelBuilder.Entity<ProfileGroupMembership>(e =>
        {
            e.HasKey(m => new { m.ProfileId, m.GroupId });
            e.Property(m => m.MembershipSource).HasConversion<string>().HasMaxLength(16);
        });
    }
}
