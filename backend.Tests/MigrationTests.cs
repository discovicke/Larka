using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace backend.Tests;

public class MigrationTests
{
    private const string _beforeContentModel = "20261023094126_InitialCreate";
    private const string _beforeContentLinkPosition = "20261026125058_RemoveAssignmentKind";
    private const string _beforeCourseTeachers = "20261029203427_AddDataPatches";

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
                SELECT "GradeScale" || '/' || array_to_string("SubmissionModes", '+') AS "Value"
                FROM "AssignmentSpecs" ORDER BY "GradeScale"
                """);
            Assert.Equal(["None/File", "PassFailDistinction/File+Repo"], specs);
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

    // Länkarna får Position i den ordning de visades innan kolumnen fanns: PublishAt, sedan titel.
    [Fact]
    public void ContentLinkPositionMigration_BackfillsExistingLinks()
    {
        var name = $"migration_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDb>().UseNpgsql(PostgresServer.CreateDatabase(name)).Options;

        try
        {
            using var db = new AppDb(options);
            db.GetService<IMigrator>().Migrate(_beforeContentLinkPosition);
            InsertLegacyLinks(db);

            db.Database.Migrate();

            var positions = Query(db, """
                SELECT content."Title" || ':' || link."Position" AS "Value"
                FROM "ContentLinks" AS link
                JOIN "Content" AS content ON content."Id" = link."ContentId"
                ORDER BY link."Position"
                """);
            Assert.Equal(["Tidigare:1", "Senare:2"], positions);
        }
        finally
        {
            PostgresServer.DropDatabase(name);
        }
    }

    // Förut var den som var lärare i en klass lärare i alla klassens kurser. Varje sådant par av användare och kurs blir
    // en rad, en gång även när läraren når kursen via flera klasser. Mentorer, elever och lärare i klasser utan
    // kurser får ingen rad.
    [Fact]
    public void CourseTeachersMigration_GivesEveryClassTeacherTheCoursesOfTheirClasses()
    {
        var name = $"migration_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDb>().UseNpgsql(PostgresServer.CreateDatabase(name)).Options;

        try
        {
            using var db = new AppDb(options);
            db.GetService<IMigrator>().Migrate(_beforeCourseTeachers);
            InsertLegacyClassTeachers(db);

            db.Database.Migrate();

            var rows = Query(db, """
                SELECT course."Code" || ':' || member."Name" AS "Value"
                FROM "CourseTeachers" AS teacher
                JOIN "Courses" AS course ON course."Id" = teacher."CourseId"
                JOIN "Users" AS member ON member."Id" = teacher."UserId"
                ORDER BY course."Code", member."Name"
                """);
            Assert.Equal(["C1:Ann", "C1:Bo", "C2:Ann", "C2:Bo"], rows);
            // Klassernas medlemskap är orörda.
            Assert.Equal(["Mentor", "Student", "Teacher", "Teacher", "Teacher", "Teacher"], Query(db, """
                SELECT "Role" AS "Value" FROM "Enrollments" ORDER BY "Role"
                """));
        }
        finally
        {
            PostgresServer.DropDatabase(name);
        }
    }

    // Tillbaka till modellen före: tabellen försvinner och klasslärarna är kvar i sina klasser, så ingen tappar något.
    [Fact]
    public void CourseTeachersMigration_CanBeRolledBack()
    {
        var name = $"migration_{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<AppDb>().UseNpgsql(PostgresServer.CreateDatabase(name)).Options;

        try
        {
            using var db = new AppDb(options);
            db.GetService<IMigrator>().Migrate(_beforeCourseTeachers);
            InsertLegacyClassTeachers(db);
            db.Database.Migrate();

            db.GetService<IMigrator>().Migrate(_beforeCourseTeachers);

            Assert.Equal(["0"], Query(db, """
                SELECT count(*)::text AS "Value" FROM information_schema.tables WHERE table_name = 'CourseTeachers'
                """));
            Assert.Equal(["4"], Query(db, """
                SELECT count(*)::text AS "Value" FROM "Enrollments" WHERE "Role" = 'Teacher'
                """));
        }
        finally
        {
            PostgresServer.DropDatabase(name);
        }
    }

    // Klass 1 läser C1 och C2, klass 2 läser C2 och klass 3 inga kurser. Ann är lärare i klass 1 och 2, Bo i klass 1,
    // Cia i klass 3 och Dan är mentor i klass 1.
    private static void InsertLegacyClassTeachers(AppDb db)
    {
        var school = Guid.NewGuid();
        var (ann, bo, cia, dan, eli) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (first, second, third) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var (c1, c2) = (Guid.NewGuid(), Guid.NewGuid());
        Execute(db, $"""
            INSERT INTO "Schools" ("Id", "Name") VALUES ('{school}', 'Skola');
            INSERT INTO "Users" ("Id", "SchoolId", "Name", "Email", "Role") VALUES
                ('{ann}', '{school}', 'Ann', 'ann@skola.se', 'Teacher'),
                ('{bo}', '{school}', 'Bo', 'bo@skola.se', 'Teacher'),
                ('{cia}', '{school}', 'Cia', 'cia@skola.se', 'Teacher'),
                ('{dan}', '{school}', 'Dan', 'dan@skola.se', 'Teacher'),
                ('{eli}', '{school}', 'Eli', 'eli@skola.se', 'Student');
            INSERT INTO "Groups" ("Id", "SchoolId", "Type", "Name", "Term") VALUES
                ('{first}', '{school}', 'Class', 'Klass 1', 'HT25'),
                ('{second}', '{school}', 'Class', 'Klass 2', 'HT25'),
                ('{third}', '{school}', 'Class', 'Klass 3', 'HT25');
            INSERT INTO "Courses" ("Id", "SchoolId", "Title", "Code") VALUES
                ('{c1}', '{school}', 'Kurs 1', 'C1'),
                ('{c2}', '{school}', 'Kurs 2', 'C2');
            INSERT INTO "GroupCourses" ("GroupId", "CourseId") VALUES
                ('{first}', '{c1}'), ('{first}', '{c2}'), ('{second}', '{c2}');
            INSERT INTO "Enrollments" ("UserId", "GroupId", "Role") VALUES
                ('{ann}', '{first}', 'Teacher'), ('{ann}', '{second}', 'Teacher'),
                ('{bo}', '{first}', 'Teacher'), ('{cia}', '{third}', 'Teacher'),
                ('{dan}', '{first}', 'Mentor'), ('{eli}', '{first}', 'Student');
            """);
    }

    // Lägger in två länkar i samma modul, där den som publiceras först också ska visas först.
    private static void InsertLegacyLinks(AppDb db)
    {
        var school = Guid.NewGuid();
        var teacher = Guid.NewGuid();
        var course = Guid.NewGuid();
        var module = Guid.NewGuid();
        var earlier = Guid.NewGuid();
        var later = Guid.NewGuid();
        Execute(db, $"""
            INSERT INTO "Schools" ("Id", "Name") VALUES ('{school}', 'Skola');
            INSERT INTO "Users" ("Id", "SchoolId", "Name", "Email", "Role")
                VALUES ('{teacher}', '{school}', 'Lärare', 'larare@skola.se', 'Teacher');
            INSERT INTO "Courses" ("Id", "SchoolId", "Title", "Code")
                VALUES ('{course}', '{school}', 'Kurs', 'KURS');
            INSERT INTO "Modules" ("Id", "CourseId", "Title", "Description", "Position")
                VALUES ('{module}', '{course}', 'Vecka 1', '', 1);
            INSERT INTO "Content" ("Id", "AuthorId", "Type", "Title", "BodyMd", "Status", "CreatedAt")
                VALUES ('{earlier}', '{teacher}', 'Theory', 'Tidigare', '', 'Published', now()),
                       ('{later}', '{teacher}', 'Theory', 'Senare', '', 'Published', now());
            INSERT INTO "ContentLinks" ("Id", "ContentId", "CourseId", "ModuleId", "PublishAt", "IsPinned")
                VALUES ('{Guid.NewGuid()}', '{later}', '{course}', '{module}', '2026-01-02T08:00:00Z', false),
                       ('{Guid.NewGuid()}', '{earlier}', '{course}', '{module}', '2026-01-01T08:00:00Z', false);
            """);
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
