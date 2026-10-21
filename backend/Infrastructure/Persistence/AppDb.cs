using Backend.Domain.Entities;
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
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<AssignmentConfig> AssignmentConfigs => Set<AssignmentConfig>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<LabAssignment> Labs => Set<LabAssignment>();
    public DbSet<StudentLabRepo> LabRepos => Set<StudentLabRepo>();
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
    }

    protected override void OnModelCreating(ModelBuilder m)
    {
        m.Entity<Enrollment>().HasKey(e => new { e.UserId, e.GroupId });
        m.Entity<GroupCourse>().HasKey(g => new { g.GroupId, g.CourseId });
        m.Entity<AssignmentConfig>().HasKey(a => a.PostId);
        m.Entity<Post>().HasOne(p => p.Assignment).WithOne().HasForeignKey<AssignmentConfig>(a => a.PostId);
        m.Entity<Submission>().HasOne(s => s.Grade).WithOne().HasForeignKey<Grade>(g => g.SubmissionId);
        m.Entity<User>().HasIndex(u => u.Email).IsUnique();
    }
}
