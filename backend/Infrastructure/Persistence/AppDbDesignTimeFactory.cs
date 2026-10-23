using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Backend.Infrastructure.Persistence;

/// <summary>
/// Används av `dotnet ef migrations add`. Migrationerna skrivs alltid för Postgres, oavsett vad
/// appsettings säger, och verktyget behöver ingen databas för att skapa dem.
/// </summary>
public class AppDbDesignTimeFactory : IDesignTimeDbContextFactory<AppDb>
{
    public AppDb CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDb>().UseNpgsql("Host=localhost").Options);
}
