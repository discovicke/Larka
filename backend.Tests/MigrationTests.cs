using Backend.Domain.Enums;
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

    // #492: admin undervisar aldrig. Migrationen tar bort lärarrader för admin och elever, men inte för lärare.
    [Fact]
    public async Task RemoveAdminCourseTeachers_KeepsOnlyTeachersAsCourseTeachers()
    {
        var name = $"tests_{Guid.NewGuid():N}";
        var connectionString = PostgresServer.CreateDatabase(name);
        try
        {
            await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(connectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261103154551_AddPersonalDueAt");

            var school = await TestData.AddSchoolAsync(db);
            var course = await TestData.AddCourseAsync(db, school.Id, placeholderTeaches: false);
            var teacher = await TestData.AddUserAsync(db, school.Id, UserRole.Teacher);
            var admin = await TestData.AddUserAsync(db, school.Id, UserRole.Admin);
            await TestData.AddCourseTeacherAsync(db, course, teacher);
            await TestData.AddCourseTeacherAsync(db, course, admin);

            await migrator.MigrateAsync("20261104091255_RemoveAdminCourseTeachers");

            var left = await db.CourseTeachers.AsNoTracking()
                .Where(row => row.CourseId == course.Id).Select(row => row.UserId).ToListAsync();
            Assert.Equal([teacher.Id], left);
        }
        finally
        {
            PostgresServer.DropDatabase(name);
        }
    }
}
