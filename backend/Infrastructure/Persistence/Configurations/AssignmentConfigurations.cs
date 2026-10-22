using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Elevernas arbete ska inte försvinna av misstag: en utdelning (ContentLink) med inlämningar går inte att
// radera. Det en inlämning äger (filer, repo, betyg) raderas med den.

public class AssignmentSpecConfiguration : IEntityTypeConfiguration<AssignmentSpec>
{
    public void Configure(EntityTypeBuilder<AssignmentSpec> b)
    {
        b.HasKey(a => a.ContentId);
    }
}

public class SubmissionConfiguration : IEntityTypeConfiguration<Submission>
{
    public void Configure(EntityTypeBuilder<Submission> b)
    {
        b.HasOne<ContentLink>().WithMany().HasForeignKey(s => s.ContentLinkId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.StudentId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => new { s.ContentLinkId, s.StudentId }).IsUnique();

        b.HasMany(s => s.Files).WithOne().HasForeignKey(f => f.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.Repo).WithOne().HasForeignKey<StudentRepo>(r => r.SubmissionId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne(s => s.Grade).WithOne().HasForeignKey<Grade>(g => g.SubmissionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StudentRepoConfiguration : IEntityTypeConfiguration<StudentRepo>
{
    public void Configure(EntityTypeBuilder<StudentRepo> b)
    {
        b.HasKey(r => r.SubmissionId);
    }
}

public class GradeConfiguration : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> b)
    {
        b.HasKey(g => g.SubmissionId);
        b.HasOne<User>().WithMany().HasForeignKey(g => g.GradedById).OnDelete(DeleteBehavior.Restrict);
    }
}
