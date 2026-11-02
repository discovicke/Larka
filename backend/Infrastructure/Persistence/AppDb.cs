using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Persistence;

public class AppDb(DbContextOptions<AppDb> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<School> Schools => Set<School>();
    public DbSet<Course> Courses => Set<Course>();
    public DbSet<Module> Modules => Set<Module>();
    public DbSet<GroupCourse> GroupCourses => Set<GroupCourse>();
    public DbSet<CourseTeacher> CourseTeachers => Set<CourseTeacher>();
    public DbSet<Group> Groups => Set<Group>();
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();
    public DbSet<Content> Content => Set<Content>();
    public DbSet<ContentLink> ContentLinks => Set<ContentLink>();
    public DbSet<ContentLinkDependency> ContentLinkDependencies => Set<ContentLinkDependency>();
    public DbSet<PartCompletion> PartCompletions => Set<PartCompletion>();
    public DbSet<ChecklistTick> ChecklistTicks => Set<ChecklistTick>();
    public DbSet<AssignmentSpec> AssignmentSpecs => Set<AssignmentSpec>();
    public DbSet<AssignmentRequirement> AssignmentRequirements => Set<AssignmentRequirement>();
    public DbSet<AssignmentGroup> AssignmentGroups => Set<AssignmentGroup>();
    public DbSet<AssignmentGroupMember> AssignmentGroupMembers => Set<AssignmentGroupMember>();
    public DbSet<Submission> Submissions => Set<Submission>();
    public DbSet<SubmissionFile> SubmissionFiles => Set<SubmissionFile>();
    public DbSet<StudentRepo> StudentRepos => Set<StudentRepo>();
    public DbSet<StudentRepoCheckRun> StudentRepoCheckRuns => Set<StudentRepoCheckRun>();
    public DbSet<StudentHelpRequest> StudentHelpRequests => Set<StudentHelpRequest>();
    public DbSet<Grade> Grades => Set<Grade>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<DataPatch> DataPatches => Set<DataPatch>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SchoolGitHubInstallation> SchoolGitHubInstallations => Set<SchoolGitHubInstallation>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        // Enums sparas som text ("Student" i stället för 0), så tabellerna går att läsa direkt i databasen.
        builder.Properties<UserRole>().HaveConversion<string>();
        builder.Properties<GroupType>().HaveConversion<string>();
        builder.Properties<GroupRole>().HaveConversion<string>();
        builder.Properties<ContentType>().HaveConversion<string>();
        builder.Properties<ContentStatus>().HaveConversion<string>();
        builder.Properties<CheckStatus>().HaveConversion<string>();
        builder.Properties<GradeScale>().HaveConversion<string>();
        builder.Properties<GradeLevel>().HaveConversion<string>();
        builder.Properties<SubmissionStatus>().HaveConversion<string>();
        builder.Properties<CompletionRule>().HaveConversion<string>();
        builder.Properties<NotificationKind>().HaveConversion<string>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Relationer, nycklar och unika regler ligger i Configurations/, en klass per tabell.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDb).Assembly);
    }
}
