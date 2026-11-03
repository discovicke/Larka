using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

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

    // RemoveMentorRole tar bort medlemskap med den borttagna rollen Mentor, och rör inga andra roller.
    [Fact]
    public async Task RemoveMentorRole_DeletesMentorEnrollments_AndKeepsTheRest()
    {
        var connectionString = PostgresServer.CreateDatabase($"tests_{Guid.NewGuid():N}");
        await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(connectionString).Options);
        var migrator = db.GetService<IMigrator>();
        var migrations = db.Database.GetMigrations().ToList();
        var previous = migrations[migrations.IndexOf(migrations.Single(id => id.EndsWith("_RemoveMentorRole"))) - 1];
        await migrator.MigrateAsync(previous);

        // Utan främmande nycklar, så att raderna räcker utan användare och grupper.
        await db.Database.ExecuteSqlRawAsync(
            """
            SET session_replication_role = replica;
            INSERT INTO "Enrollments" ("UserId", "GroupId", "Role") VALUES
              (gen_random_uuid(), gen_random_uuid(), 'Mentor'),
              (gen_random_uuid(), gen_random_uuid(), 'Teacher'),
              (gen_random_uuid(), gen_random_uuid(), 'Student');
            """);

        await migrator.MigrateAsync();

        var roles = await db.Database.SqlQueryRaw<string>("""SELECT "Role" AS "Value" FROM "Enrollments" ORDER BY 1""")
            .ToListAsync();
        Assert.Equal(["Student", "Teacher"], roles);
    }
}
