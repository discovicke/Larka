using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Användaren som PlaceholderCurrentUser pekar på. Tas bort när riktig inloggning finns.

public class PlaceholderSchoolSeed : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> b)
    {
        b.HasData(new School { Id = PlaceholderCurrentUser.PlaceholderSchoolId, Name = "Placeholder-skolan" });
    }
}

public class PlaceholderUserSeed : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.HasData(new User
        {
            Id = PlaceholderCurrentUser.PlaceholderUserId,
            SchoolId = PlaceholderCurrentUser.PlaceholderSchoolId,
            Name = "Placeholder",
            Email = "placeholder@rebornlms.local",
            Role = UserRole.Admin
        });
    }
}
