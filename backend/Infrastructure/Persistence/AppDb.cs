using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Backend.Infrastructure.Persistence;

public class AppDb : DbContext
{
    public AppDb(DbContextOptions<AppDb> o) : base(o)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<GroupCourse> GroupCourses => Set<GroupCourse>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Content> Content => Set<Content>();
    public DbSet<ContentLink> ContentLinks => Set<ContentLink>();
    public DbSet<AssignmentSpec> AssignmentSpecs => Set<AssignmentSpec>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();
    public DbSet<StudentRepo> StudentRepos => Set<StudentRepo>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void ConfigureConventions(ModelConfigurationBuilder b)
    {
        // SQLite kan inte sortera/jämföra DateTimeOffset, så där lagras de som tal. Postgres hanterar dem själv.
        if (Database.IsSqlite())
        {
            b.Properties<DateTimeOffset>().HaveConversion<DateTimeOffsetToBinaryConverter>();
            b.Properties<DateTimeOffset?>().HaveConversion<DateTimeOffsetToBinaryConverter>();
        }

        // Enums sparas som text ("Student" i stället för 0), så tabellerna går att läsa direkt i databasen.
        b.Properties<UserRole>().HaveConversion<string>();
        b.Properties<GroupType>().HaveConversion<string>();
        b.Properties<GroupRole>().HaveConversion<string>();
        b.Properties<ContentType>().HaveConversion<string>();
        b.Properties<ContentStatus>().HaveConversion<string>();
        b.Properties<CheckStatus>().HaveConversion<string>();
        b.Properties<SubmissionMode>().HaveConversion<string>();
        b.Properties<SubmissionStatus>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        // Relationer, nycklar och unika regler ligger i Configurations/, en klass per tabell.
        m.ApplyConfigurationsFromAssembly(typeof(AppDb).Assembly);
    }
}
