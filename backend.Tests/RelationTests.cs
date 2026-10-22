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
public class RelationTests : IClassFixture<TestWebApplicationFactory>
{
    // Tabeller som inte har gått över till den nya modellen än (#38 och #39). Tas bort därifrån när de gör det.
    private static readonly HashSet<string> _notMigratedYet =
    [
        nameof(Post), nameof(Comment), nameof(Attachment), nameof(Submission), nameof(Grade),
        nameof(LabAssignment), nameof(StudentLabRepo)
    ];

    private readonly TestWebApplicationFactory _factory;

    public RelationTests(TestWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void EveryIdColumn_IsAForeignKey()
    {
        using var scope = _factory.Services.CreateScope();
        var model = scope.ServiceProvider.GetRequiredService<AppDb>().Model;

        var missing = model.GetEntityTypes()
            .Where(e => !_notMigratedYet.Contains(e.ClrType.Name))
            .SelectMany(e => e.GetProperties().Select(p => (Entity: e, Property: p)))
            .Where(x => x.Property.Name != "Id" && x.Property.Name.EndsWith("Id") && !x.Property.IsForeignKey())
            .Select(x => $"{x.Entity.ClrType.Name}.{x.Property.Name}")
            .ToList();

        Assert.Empty(missing);
    }

    [Fact]
    public async Task Enrollment_InGroupThatDoesNotExist_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var user = await TestData.AddUserAsync(db);

        db.Enrollments.Add(new Enrollment { UserId = user.Id, GroupId = Guid.NewGuid() });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Course_WithSameCodeAtSameSchool_IsRejected()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var school = await TestData.AddSchoolAsync(db);
        db.Courses.Add(new Course { SchoolId = school.Id, Title = "C#", Code = "NET-101" });
        await db.SaveChangesAsync();

        db.Courses.Add(new Course { SchoolId = school.Id, Title = "C# igen", Code = "NET-101" });

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task School_WithGroups_CannotBeDeleted()
    {
        Guid schoolId;
        using (var setup = _factory.Services.CreateScope())
        {
            var setupDb = setup.ServiceProvider.GetRequiredService<AppDb>();
            schoolId = (await TestData.AddSchoolAsync(setupDb)).Id;
            await TestData.AddGroupAsync(setupDb, schoolId);
        }

        // Ny context som inte känner till gruppen, så det är databasen som säger nej och inte EF.
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        db.Schools.Remove(await db.Schools.SingleAsync(s => s.Id == schoolId));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EnrollmentRole_IsStoredAsText()
    {
        using var scope = _factory.Services.CreateScope();
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
}
