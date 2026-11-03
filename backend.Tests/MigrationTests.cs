using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests;

public class MigrationTests
{
    // Faller om någon ändrat en entitet eller konfiguration utan att köra `dotnet ef migrations add`.
    // Då skulle prod-databasen inte få ändringen. Behöver ingen databas.
    [Fact]
    public void Migrations_match_the_model()
    {
        using var db = new AppDbDesignTimeFactory().CreateDbContext([]);

        Assert.False(
            db.Database.HasPendingModelChanges(),
            "Modellen har ändringar som saknar migration. Kör: dotnet ef migrations add <Namn> --project backend");
    }

}
