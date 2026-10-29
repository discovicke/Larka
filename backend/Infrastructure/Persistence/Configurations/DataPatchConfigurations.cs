using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Backend.Infrastructure.Persistence.Configurations;

public class DataPatchConfiguration : IEntityTypeConfiguration<DataPatch>
{
    public void Configure(EntityTypeBuilder<DataPatch> builder)
    {
        builder.HasKey(patch => patch.Id);
        builder.Property(patch => patch.Id).HasMaxLength(100);
    }
}
