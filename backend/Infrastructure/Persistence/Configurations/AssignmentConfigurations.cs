using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Elevernas arbete ska inte försvinna av misstag: en utdelning (ContentLink) med inlämningar går inte att
// radera. Det en inlämning äger (filer, repo, betyg) raderas med den.

public class AssignmentSpecConfiguration : IEntityTypeConfiguration<AssignmentSpec>
{
    public void Configure(EntityTypeBuilder<AssignmentSpec> builder)
    {
        builder.HasKey(spec => spec.ContentId);

        // Listan sparas som en textarray ("Text", "File"), som övriga enums, så den går att läsa i databasen.
        builder.PrimitiveCollection(spec => spec.SubmissionModes).ElementType().HasConversion<string>();

        // Standardvärdet gäller rader som skapas utanför EF och de som fanns innan kolumnen kom.
        builder.Property(spec => spec.CompletionRule).HasDefaultValue(CompletionRule.Submitted);
    }
}

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> builder)
    {
        builder.HasOne<ContentLink>().WithMany()
            .HasForeignKey(submission => submission.ContentLinkId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(submission => submission.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(submission => new { submission.ContentLinkId, submission.StudentId }).IsUnique();

        builder.HasMany(submission => submission.Files).WithOne()
            .HasForeignKey(file => file.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(submission => submission.Repo).WithOne()
            .HasForeignKey<StudentRepo>(repo => repo.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(submission => submission.Grade).WithOne()
            .HasForeignKey<Grade>(grade => grade.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StudentRepoConfiguration : IEntityTypeConfiguration<StudentRepo>
{
    public void Configure(EntityTypeBuilder<StudentRepo> builder)
    {
        builder.HasKey(repo => repo.SubmissionId);
    }
}

public class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.HasKey(grade => grade.SubmissionId);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(grade => grade.GradedById).OnDelete(DeleteBehavior.Restrict);
    }
}
