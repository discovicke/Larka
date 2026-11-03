using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

public class RowLevelSecurityTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    private const string _tablesWithoutRls =
        """
        SELECT c.relname AS "Value"
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relkind IN ('r', 'p') AND NOT c.relrowsecurity
        """;

    [Fact]
    public async Task Startup_TurnsOnRowLevelSecurityForEveryTable()
    {
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();

        var open = await db.Database.SqlQueryRaw<string>(_tablesWithoutRls).ToListAsync();

        Assert.Empty(open);
    }

    [Fact]
    public async Task EnableOnAllTables_TurnsItBackOn_AndTheAppStillReadsItsTables()
    {
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Database.ExecuteSqlRawAsync("""ALTER TABLE public."Users" DISABLE ROW LEVEL SECURITY""");

        RowLevelSecurity.EnableOnAllTables(db);

        Assert.Empty(await db.Database.SqlQueryRaw<string>(_tablesWithoutRls).ToListAsync());
        // Appen är tabellernas ägare och går förbi RLS, så den läser som vanligt.
        Assert.True(await db.Users.AnyAsync());
    }
}
