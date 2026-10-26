using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace backend.Tests;

public class MigrationTests
{
    private const string _beforeContentModel = "20261023094126_InitialCreate";

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

    // Poäng blir betyg, MaxScore blir skala och FileOrRepo blir File + Repo, så att data som redan finns överlever.
    [Fact]
    public void ContentModelMigration_ConvertsExistingAssignmentsAndGrades()
    {
        var name = $"migration_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDb>().UseNpgsql(PostgresServer.CreateDatabase(name)).Options;

        try
        {
            using var db = new AppDb(options);
            db.GetService<IMigrator>().Migrate(_beforeContentModel);
            InsertLegacyAssignments(db);

            db.Database.Migrate();

            var specs = Query(db, """
                SELECT "Kind" || '/' || "GradeScale" || '/' || array_to_string("SubmissionModes", '+') AS "Value"
                FROM "AssignmentSpecs" ORDER BY "GradeScale"
                """);
            Assert.Equal(["Lab/None/File", "Lab/PassFailDistinction/File+Repo"], specs);
            var levels = Query(db, """
                SELECT COALESCE("Level", 'none') AS "Value" FROM "Grades" ORDER BY "Feedback"
                """);
            Assert.Equal(["Distinction", "Pass", "Fail", "none"], levels);
        }
        finally
        {
            PostgresServer.DropDatabase(name);
        }
    }

    // Lägger in två uppgifter (en med poäng och FileOrRepo, en utan poäng) och fyra betyg.
    private static void InsertLegacyAssignments(AppDb db)
    {
        var school = Guid.NewGuid();
        var teacher = Guid.NewGuid();
        var graded = Guid.NewGuid();
        var ungraded = Guid.NewGuid();
        var group = Guid.NewGuid();
        var link = Guid.NewGuid();
        Execute(db, $"""
            INSERT INTO "Schools" ("Id", "Name") VALUES ('{school}', 'Skola');
            INSERT INTO "Users" ("Id", "SchoolId", "Name", "Email", "Role")
                VALUES ('{teacher}', '{school}', 'Lärare', 'larare@skola.se', 'Teacher');
            INSERT INTO "Groups" ("Id", "SchoolId", "Type", "Name", "Term")
                VALUES ('{group}', '{school}', 'Class', 'NET25', 'HT25');
            INSERT INTO "Content" ("Id", "AuthorId", "Type", "Title", "BodyMd", "Status", "CreatedAt")
                VALUES ('{graded}', '{teacher}', 'Assignment', 'Med poäng', '', 'Published', now()),
                       ('{ungraded}', '{teacher}', 'Assignment', 'Utan poäng', '', 'Published', now());
            INSERT INTO "AssignmentSpecs" ("ContentId", "MaxScore", "SubmissionMode", "TemplateRepo")
                VALUES ('{graded}', 10, 'FileOrRepo', 'org/mall'), ('{ungraded}', NULL, 'File', NULL);
            INSERT INTO "ContentLinks" ("Id", "ContentId", "GroupId", "PublishAt", "IsPinned")
                VALUES ('{link}', '{graded}', '{group}', now(), false);
            """);

        // Poäng 9, 5 och 2 av 10 blir Distinction, Pass och Fail. Komplettering utan poäng blir utan betyg.
        int?[] scores = [9, 5, 2, null];
        for (var i = 0; i < scores.Length; i++)
        {
            var student = Guid.NewGuid();
            var submission = Guid.NewGuid();
            var score = scores[i]?.ToString() ?? "NULL";
            Execute(db, $"""
                INSERT INTO "Users" ("Id", "SchoolId", "Name", "Email", "Role")
                    VALUES ('{student}', '{school}', 'Elev {i}', 'elev{i}@skola.se', 'Student');
                INSERT INTO "Submissions" ("Id", "ContentLinkId", "StudentId", "Status")
                    VALUES ('{submission}', '{link}', '{student}', 'Graded');
                INSERT INTO "Grades" ("SubmissionId", "GradedById", "Score", "Feedback", "GradedAt")
                    VALUES ('{submission}', '{teacher}', {score}, 'Betyg {i}', now());
                """);
        }
    }

    private static void Execute(AppDb db, string sql) => db.Database.ExecuteSqlRaw(sql);

    private static List<string> Query(AppDb db, string sql) => db.Database.SqlQueryRaw<string>(sql).ToList();
}
