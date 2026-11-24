using Microsoft.EntityFrameworkCore;
using Switchly.Domain.Bookmarks;
using Switchly.Domain.Downloads;
using Switchly.Domain.Groups;
using Switchly.Domain.History;
using Switchly.Domain.Profiles;
using Switchly.Domain.Tabs;

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

    public DbSet<Bookmark> Bookmarks => Set<Bookmark>();

    public DbSet<HistoryEntry> HistoryEntries => Set<HistoryEntry>();

    public DbSet<DownloadRecord> Downloads => Set<DownloadRecord>();

    public DbSet<BrowserTabSession> TabSessions => Set<BrowserTabSession>();

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

        modelBuilder.Entity<Bookmark>(e =>
        {
            e.HasKey(b => b.BookmarkId);
            e.Property(b => b.Title).IsRequired().HasMaxLength(512);
            e.Property(b => b.Url).HasMaxLength(2048);
            e.HasIndex(b => new { b.ProfileId, b.ParentBookmarkId, b.Position });

            e.HasOne<BrowserProfile>()
                .WithMany()
                .HasForeignKey(b => b.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne<Bookmark>()
                .WithMany()
                .HasForeignKey(b => b.ParentBookmarkId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<HistoryEntry>(e =>
        {
            e.HasKey(h => h.HistoryId);
            e.Property(h => h.Url).IsRequired().HasMaxLength(2048);
            e.Property(h => h.Title).HasMaxLength(512);
            e.Property(h => h.TransitionKind).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(h => new { h.ProfileId, h.VisitedAtUtc });

            e.HasOne<BrowserProfile>()
                .WithMany()
                .HasForeignKey(h => h.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DownloadRecord>(e =>
        {
            e.HasKey(d => d.DownloadId);
            e.Property(d => d.SuggestedFileName).IsRequired().HasMaxLength(255);
            e.Property(d => d.TargetPath).IsRequired().HasMaxLength(2048);
            e.Property(d => d.MimeType).HasMaxLength(255);
            e.Property(d => d.State).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(d => new { d.ProfileId, d.StartedAtUtc });

            e.HasOne<BrowserProfile>()
                .WithMany()
                .HasForeignKey(d => d.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BrowserTabSession>(e =>
        {
            e.HasKey(t => t.TabId);
            e.Property(t => t.Url).IsRequired().HasMaxLength(2048);
            e.Property(t => t.Title).HasMaxLength(512);
            e.Property(t => t.RestoreState).HasConversion<string>().HasMaxLength(16);
            e.HasIndex(t => new { t.ProfileId, t.WindowId, t.Position });

            e.HasOne<BrowserProfile>()
                .WithMany()
                .HasForeignKey(t => t.ProfileId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
