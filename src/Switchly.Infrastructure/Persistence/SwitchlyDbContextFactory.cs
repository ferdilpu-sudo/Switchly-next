using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Switchly.Infrastructure.Persistence;

/// <summary>Design-time factory so `dotnet ef` can generate migrations without a host.</summary>
public sealed class SwitchlyDbContextFactory : IDesignTimeDbContextFactory<SwitchlyDbContext>
{
    public SwitchlyDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<SwitchlyDbContext>()
            .UseSqlite("Data Source=switchly-design.db")
            .Options;
        return new SwitchlyDbContext(options);
    }
}
