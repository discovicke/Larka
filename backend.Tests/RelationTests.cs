using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace backend.Tests;

/// <summary>
/// Kontrollerar att relationerna i ER-modellen (docs/er-diagram.md) finns som riktiga foreign keys
/// och att databasen faktiskt stoppar ogiltiga rader.
/// </summary>
public class RelationTests(TestWebApplicationFactory factory) : IClassFixture<TestWebApplicationFactory>
{
    [Fact]
    public void EveryIdColumn_IsAForeignKey()
    {
        using var scope = factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDb>().Model;

        var missing = model.GetEntityTypes()
            .SelectMany(entity => entity.GetProperties().Select(property => (Entity: entity, Property: property)))
            .Where(column =>
                column.Property.Name != "Id"
                && column.Property.Name != "InstallationId"
                && column.Property.Name.EndsWith("Id")
                && !column.Property.IsForeignKey())
            .Select(column => $"{column.Entity.ClrType.Name}.{column.Property.Name}")
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public async Task Enrollment_InGroupThatDoesNotExist_IsRejected()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await TestData.AddUserAsync(db);

        db.Enrollments.Add(new Enrollment { UserId = user.Id, GroupId = Guid.NewGuid() });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Course_WithSameCodeAtSameSchool_IsRejected()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        db.Courses.Add(new Course { SchoolId = school.Id, Title = "C#", Code = "NET-101" });
        await db.SaveChangesAsync();

        db.Courses.Add(new Course { SchoolId = school.Id, Title = "C# igen", Code = "NET-101" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Link_ToModuleWithoutCourse_IsRejected()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var group = await TestData.AddGroupAsync(db);
        var course = await TestData.AddCourseAsync(db, group.SchoolId);
        var author = await TestData.AddUserAsync(db, group.SchoolId, UserRole.Teacher);
        var module = new Module { CourseId = course.Id, Title = "Vecka 1", Position = 1 };
        var content = new Content { AuthorId = author.Id, Type = ContentType.Theory, Title = "Teori" };
        db.AddRange(module, content);
        await db.SaveChangesAsync();

        db.ContentLinks.Add(new ContentLink { ContentId = content.Id, GroupId = group.Id, ModuleId = module.Id });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task School_WithGroups_CannotBeDeleted()
    {
        Guid schoolId;
        using (var setup = factory.Services.CreateScope())
        {
            var setupDb = setup.ServiceProvider.GetRequiredService<AppDb>();
            schoolId = (await TestData.AddSchoolAsync(setupDb)).Id;
            await TestData.AddGroupAsync(setupDb, schoolId);
        }

        // Ny context som inte känner till gruppen, så det är databasen som säger nej och inte EF.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.Schools.Remove(await db.Schools.SingleAsync(school => school.Id == schoolId));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EnrollmentRole_IsStoredAsText()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await TestData.AddUserAsync(db);
        var group = await TestData.AddGroupAsync(db, user.SchoolId);
        db.Enrollments.Add(new Enrollment { UserId = user.Id, GroupId = group.Id, Role = GroupRole.Teacher });
        await db.SaveChangesAsync();

        var stored = await db.Database
            .SqlQuery<string>($"SELECT \"Role\" AS \"Value\" FROM \"Enrollments\" WHERE \"UserId\" = {user.Id}")
            .SingleAsync();

        Assert.Equal("Teacher", stored);
    }

    [Fact]
    public async Task SecondSubmission_ForSameStudentAndLink_IsRejected()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var (link, student) = await AddLinkWithStudentAsync(db);
        db.Submissions.Add(new Submission { ContentLinkId = link.Id, StudentId = student.Id });
        await db.SaveChangesAsync();

        db.Submissions.Add(new Submission { ContentLinkId = link.Id, StudentId = student.Id });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Link_WithSubmissions_CannotBeDeleted()
    {
        Guid linkId;
        using (var setup = factory.Services.CreateScope())
        {
            var setupDb = setup.ServiceProvider.GetRequiredService<AppDb>();
            var (link, student) = await AddLinkWithStudentAsync(setupDb);
            setupDb.Submissions.Add(new Submission { ContentLinkId = link.Id, StudentId = student.Id });
            await setupDb.SaveChangesAsync();
            linkId = link.Id;
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.ContentLinks.Remove(await db.ContentLinks.SingleAsync(link => link.Id == linkId));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Notification_KeepsItsRow_WhenContentIsDeleted()
    {
        Guid notificationId, contentId;
        using (var setup = factory.Services.CreateScope())
        {
            var setupDb = setup.ServiceProvider.GetRequiredService<AppDb>();
            var user = await TestData.AddUserAsync(setupDb);
            var content = new Content { AuthorId = user.Id, Type = ContentType.News, Title = "Nyhet" };
            var notification = new Notification
            {
                UserId = user.Id,
                Kind = NotificationKind.NewsPublished,
                ContentId = content.Id
            };
            setupDb.Content.Add(content);
            setupDb.Notifications.Add(notification);
            await setupDb.SaveChangesAsync();
            (notificationId, contentId) = (notification.Id, content.Id);
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        await db.Content.Where(content => content.Id == contentId).ExecuteDeleteAsync();

        var kept = await db.Notifications.SingleAsync(notification => notification.Id == notificationId);
        Assert.Null(kept.ContentId);
    }

    private static async Task<(ContentLink Link, User Student)> AddLinkWithStudentAsync(AppDb db)
    {
        var group = await TestData.AddGroupAsync(db);
        var student = await TestData.AddMemberAsync(db, group);
        var content = new Content { AuthorId = student.Id, Type = ContentType.Assignment, Title = "Labb" };
        var link = new ContentLink { ContentId = content.Id, GroupId = group.Id };
        db.Content.Add(content);
        db.ContentLinks.Add(link);
        await db.SaveChangesAsync();
        return (link, student);
    }
}
