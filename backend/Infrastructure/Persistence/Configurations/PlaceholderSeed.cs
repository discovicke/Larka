using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

// Skolan och admin-användaren som migrationen lägger in. Finns alltid. Id:na är fasta, så att de går att
// använda i exempeldata, i migrationen och i testerna.
public static class PlaceholderIds
{
    public static readonly Guid SchoolId = new("00000000-0000-0000-0000-00000000000a");
    public static readonly Guid UserId = new("00000000-0000-0000-0000-000000000001");
}

public class PlaceholderSchoolSeed : IEntityTypeConfiguration<School>
{
    public void Configure(EntityTypeBuilder<School> builder)
    {
        builder.HasData(new School { Id = PlaceholderIds.SchoolId, Name = "Placeholder-skolan" });
    }
}

public class PlaceholderUserSeed : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasData(new User
        {
            Id = PlaceholderIds.UserId,
            SchoolId = PlaceholderIds.SchoolId,
            Name = "Placeholder",
            Email = "placeholder@larka.local",
            Role = UserRole.Admin
        });
    }
}
