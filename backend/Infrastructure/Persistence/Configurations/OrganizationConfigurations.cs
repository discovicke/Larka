using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Skolor, grupper, kurser och användare delas av mycket annat, så de går inte att radera
// så länge något pekar på dem (Restrict). EF skapar ett index för varje foreign key.

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasOne<School>().WithMany().HasForeignKey(u => u.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(u => u.Email).IsUnique();
    }
}

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> b)
    {
        b.HasOne<School>().WithMany().HasForeignKey(g => g.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(g => new { g.SchoolId, g.Name, g.Term }).IsUnique();
    }
}

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.HasOne<School>().WithMany().HasForeignKey(c => c.SchoolId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(c => new { c.SchoolId, c.Code }).IsUnique();
    }
}

// Enda kopplingen mellan kurs och grupp (M-N).
public class GroupCourseConfiguration : IEntityTypeConfiguration<GroupCourse>
{
    public void Configure(EntityTypeBuilder<GroupCourse> b)
    {
        b.HasKey(gc => new { gc.GroupId, gc.CourseId });
        b.HasOne<Group>().WithMany().HasForeignKey(gc => gc.GroupId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Course>().WithMany().HasForeignKey(gc => gc.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> b)
    {
        b.HasKey(e => new { e.UserId, e.GroupId });
        b.HasOne<User>().WithMany().HasForeignKey(e => e.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Group>().WithMany().HasForeignKey(e => e.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
