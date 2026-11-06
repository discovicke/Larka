using Backend.Domain.Entities;
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

    // #545: dubbletter från före de unika indexen tas bort, den äldsta behålls. Påminnelser om olika deadlines
    // och notiser av slag som får finnas flera gånger lämnas kvar.
    [Fact]
    public async Task OneNotificationPerPublishAndDeadline_RemovesOldDuplicates_KeepingTheOldest()
    {
        var name = $"tests_{Guid.NewGuid():N}";
        var connectionString = PostgresServer.CreateDatabase(name);
        try
        {
            await using var db = new AppDb(new DbContextOptionsBuilder<AppDb>().UseNpgsql(connectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261105144643_AddCommentNotifications");

            var school = await TestData.AddSchoolAsync(db);
            var author = await TestData.AddUserAsync(db, school.Id, UserRole.Teacher);
            var student = await TestData.AddUserAsync(db, school.Id);
            var content = new Content { AuthorId = author.Id, Type = ContentType.Assignment, Title = "Labb" };
            db.Content.Add(content);
            var start = new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero);
            var due = start.AddDays(3);
            Notification Make(NotificationKind kind, int minutes, DateTimeOffset? dueAt = null) => new()
            {
                UserId = student.Id,
                Kind = kind,
                ContentId = content.Id,
                DueAt = dueAt,
                CreatedAt = start.AddMinutes(minutes)
            };
            var oldest = Make(NotificationKind.AssignmentPublished, 0);
            var firstReminder = Make(NotificationKind.DeadlineSoon, 0, due);
            var otherDeadline = Make(NotificationKind.DeadlineSoon, 2, due.AddDays(7));
            db.Notifications.AddRange(
                oldest,
                Make(NotificationKind.AssignmentPublished, 1),
                firstReminder,
                Make(NotificationKind.DeadlineSoon, 1, due),
                otherDeadline,
                Make(NotificationKind.SubmissionGraded, 0),
                Make(NotificationKind.SubmissionGraded, 1));
            await db.SaveChangesAsync();

            await migrator.MigrateAsync("20261106091649_OneNotificationPerPublishAndDeadline");

            var left = await db.Notifications.AsNoTracking()
                .Where(notification => notification.UserId == student.Id
                    && notification.Kind != NotificationKind.SubmissionGraded)
                .Select(notification => notification.Id)
                .ToListAsync();
            Assert.Equal(
                new[] { oldest.Id, firstReminder.Id, otherDeadline.Id }.Order(),
                left.Order());
            Assert.Equal(2, await db.Notifications.CountAsync(notification =>
                notification.UserId == student.Id && notification.Kind == NotificationKind.SubmissionGraded));
        }
        finally
        {
            PostgresServer.DropDatabase(name);
        }
    }
}
