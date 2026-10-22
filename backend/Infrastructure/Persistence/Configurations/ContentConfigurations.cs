using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Det content äger (länkar, uppgiftsinställningar, bilagor, kommentarer) raderas med det (Cascade).
// Användare, grupper och kurser går inte att radera så länge content pekar på dem (Restrict).

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> b)
    {
        b.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne(c => c.Assignment).WithOne().HasForeignKey<AssignmentConfig>(a => a.ContentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContentLinkConfiguration : IEntityTypeConfiguration<ContentLink>
{
    public void Configure(EntityTypeBuilder<ContentLink> b)
    {
        b.HasOne<Content>().WithMany().HasForeignKey(l => l.ContentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Group>().WithMany().HasForeignKey(l => l.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Course>().WithMany().HasForeignKey(l => l.CourseId).OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint(
            "CK_ContentLinks_GroupOrCourse", "\"GroupId\" IS NOT NULL OR \"CourseId\" IS NOT NULL"));
    }
}

public class AssignmentConfigConfiguration : IEntityTypeConfiguration<AssignmentConfig>
{
    public void Configure(EntityTypeBuilder<AssignmentConfig> b)
    {
        b.HasKey(a => a.ContentId);
    }
}

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> b)
    {
        b.HasOne<Content>().WithMany().HasForeignKey(a => a.ContentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.HasOne<Content>().WithMany().HasForeignKey(c => c.ContentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}
