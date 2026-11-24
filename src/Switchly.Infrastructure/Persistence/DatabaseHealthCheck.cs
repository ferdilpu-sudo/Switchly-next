using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Switchly.Infrastructure.Persistence;

/// <summary>Applies migrations and runs a trivial query to confirm the DB is usable.</summary>
public sealed class DatabaseHealthCheck
{
    private readonly SwitchlyDbContext _db;
    private readonly ILogger<DatabaseHealthCheck> _logger;

    public DatabaseHealthCheck(SwitchlyDbContext db, ILogger<DatabaseHealthCheck> logger)
    {
        _db = db;
        _logger = logger;
    }

    public bool Check()
    {
        try
        {
            _db.Database.Migrate();
            var count = _db.Profiles.Count();
            _logger.LogInformation("Database health check OK. Profiles={Count}", count);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed.");
            return false;
        }
    }
}
