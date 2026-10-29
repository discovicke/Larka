using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Det content äger (länkar, AssignmentSpec, bilagor, kommentarer) raderas med det (Cascade).
// Användare, grupper och kurser går inte att radera så länge content pekar på dem (Restrict).

public class ContentConfiguration : IEntityTypeConfiguration<Content>
{
    public void Configure(EntityTypeBuilder<Content> builder)
    {
        builder.HasOne<User>().WithMany().HasForeignKey(content => content.AuthorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(content => content.Assignment).WithOne()
            .HasForeignKey<AssignmentSpec>(spec => spec.ContentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class ContentLinkConfiguration : IEntityTypeConfiguration<ContentLink>
{
    public void Configure(EntityTypeBuilder<ContentLink> builder)
    {
        builder.HasOne<Content>().WithMany().HasForeignKey(link => link.ContentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Group>().WithMany().HasForeignKey(link => link.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>().WithMany().HasForeignKey(link => link.CourseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Module>().WithMany().HasForeignKey(link => link.ModuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(link => new { link.ModuleId, link.Position });
        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "CK_ContentLinks_GroupOrCourse", "\"GroupId\" IS NOT NULL OR \"CourseId\" IS NOT NULL");
            table.HasCheckConstraint(
                "CK_ContentLinks_ModuleNeedsCourse", "\"ModuleId\" IS NULL OR \"CourseId\" IS NOT NULL");
        });
    }
}

// Beroenden hör till en vecka, så båda sidorna är ContentLinks. Raderna följer med när en del tas bort.
public class ContentLinkDependencyConfiguration : IEntityTypeConfiguration<ContentLinkDependency>
{
    public void Configure(EntityTypeBuilder<ContentLinkDependency> builder)
    {
        builder.HasKey(dependency => new { dependency.BlockedLinkId, dependency.BlockingLinkId });
        builder.HasOne<ContentLink>().WithMany()
            .HasForeignKey(dependency => dependency.BlockedLinkId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ContentLink>().WithMany()
            .HasForeignKey(dependency => dependency.BlockingLinkId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(dependency => dependency.BlockingLinkId);
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_ContentLinkDependencies_NotSelf", "\"BlockedLinkId\" <> \"BlockingLinkId\""));
    }
}

// En rad per elev och del. Klarmarkeringen är elevens egen och tas bort med delen.
public class PartCompletionConfiguration : IEntityTypeConfiguration<PartCompletion>
{
    public void Configure(EntityTypeBuilder<PartCompletion> builder)
    {
        builder.HasOne<ContentLink>().WithMany()
            .HasForeignKey(completion => completion.ContentLinkId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(completion => completion.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(completion => new { completion.ContentLinkId, completion.StudentId }).IsUnique();
    }
}

// En rad per elev, del och avbockad punkt. Tas bort med delen.
public class ChecklistTickConfiguration : IEntityTypeConfiguration<ChecklistTick>
{
    public void Configure(EntityTypeBuilder<ChecklistTick> builder)
    {
        builder.HasOne<ContentLink>().WithMany()
            .HasForeignKey(tick => tick.ContentLinkId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(tick => tick.StudentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(tick => new { tick.StudentId, tick.ContentLinkId, tick.ItemIndex }).IsUnique();
    }
}

public class AttachmentConfiguration : IEntityTypeConfiguration<Attachment>
{
    public void Configure(EntityTypeBuilder<Attachment> builder)
    {
        builder.HasOne<Content>().WithMany()
            .HasForeignKey(attachment => attachment.ContentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> builder)
    {
        builder.HasOne<Content>().WithMany()
            .HasForeignKey(comment => comment.ContentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<User>().WithMany()
            .HasForeignKey(comment => comment.AuthorId).OnDelete(DeleteBehavior.Restrict);
    }
}
