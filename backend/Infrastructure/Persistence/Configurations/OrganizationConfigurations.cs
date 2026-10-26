using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Skolor, grupper, kurser och användare delas av mycket annat, så de går inte att radera
// så länge något pekar på dem (Restrict). EF skapar ett index för varje foreign key.

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasOne<School>().WithMany().HasForeignKey(user => user.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(user => user.Email).IsUnique();
    }
}

public class GroupConfiguration : IEntityTypeConfiguration<Group>
{
    public void Configure(EntityTypeBuilder<Group> builder)
    {
        builder.HasOne<School>().WithMany().HasForeignKey(group => group.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(group => new { group.SchoolId, group.Name, group.Term }).IsUnique();
    }
}

public class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public void Configure(EntityTypeBuilder<Course> builder)
    {
        builder.HasOne<School>().WithMany().HasForeignKey(course => course.SchoolId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(course => new { course.SchoolId, course.Code }).IsUnique();
    }
}

public class ModuleConfiguration : IEntityTypeConfiguration<Module>
{
    public void Configure(EntityTypeBuilder<Module> builder)
    {
        builder.HasOne<Course>().WithMany().HasForeignKey(module => module.CourseId).OnDelete(DeleteBehavior.Restrict);
        // Inte unikt: då skulle en omsortering krocka med sig själv mitt i, eftersom raderna uppdateras en i taget.
        builder.HasIndex(module => new { module.CourseId, module.Position });
    }
}

// Enda kopplingen mellan kurs och grupp (M-N).
public class GroupCourseConfiguration : IEntityTypeConfiguration<GroupCourse>
{
    public void Configure(EntityTypeBuilder<GroupCourse> builder)
    {
        builder.HasKey(groupCourse => new { groupCourse.GroupId, groupCourse.CourseId });
        builder.HasOne<Group>().WithMany()
            .HasForeignKey(groupCourse => groupCourse.GroupId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Course>().WithMany()
            .HasForeignKey(groupCourse => groupCourse.CourseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class EnrollmentConfiguration : IEntityTypeConfiguration<Enrollment>
{
    public void Configure(EntityTypeBuilder<Enrollment> builder)
    {
        builder.HasKey(enrollment => new { enrollment.UserId, enrollment.GroupId });
        builder.HasOne<User>().WithMany()
            .HasForeignKey(enrollment => enrollment.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Group>().WithMany()
            .HasForeignKey(enrollment => enrollment.GroupId).OnDelete(DeleteBehavior.Restrict);
    }
}
