using Microsoft.EntityFrameworkCore;
using Switchly.Application.Browser;
using Switchly.Domain.Profiles;

namespace Switchly.Infrastructure.Persistence;

/// <summary>Creates short-lived DbContext instances for the singleton ProfileSessionManager.</summary>
public sealed class EfProfileSessionStore : IProfileSessionStore
{
    private readonly IDbContextFactory<SwitchlyDbContext> _factory;

    public EfProfileSessionStore(IDbContextFactory<SwitchlyDbContext> factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<BrowserProfile?> GetProfileAsync(Guid profileId, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        return await db.Profiles.AsNoTracking().FirstOrDefaultAsync(p => p.ProfileId == profileId, cancellationToken);
    }

    public async Task RecordLastOpenedAsync(Guid profileId, DateTimeOffset openedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var db = await _factory.CreateDbContextAsync(cancellationToken);
        var profile = await db.Profiles.FirstOrDefaultAsync(p => p.ProfileId == profileId, cancellationToken);
        if (profile is null)
        {
            return;
        }

        profile.LastOpenedAtUtc = openedAtUtc;
        profile.UpdatedAtUtc = openedAtUtc;
        await db.SaveChangesAsync(cancellationToken);
    }
}
