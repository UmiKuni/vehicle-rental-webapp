using CarRental.Modules.Fleet.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Fleet.Data.Configurations;

public class VehicleImageConfiguration : IEntityTypeConfiguration<VehicleImage>
{
    public void Configure(EntityTypeBuilder<VehicleImage> builder)
    {
        builder.ToTable("vehicle_images");

        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id)
            .HasColumnName("id");

        builder.Property(i => i.VehicleId)
            .HasConversion(id => id.Value, val => new VehicleId(val))
            .IsRequired()
            .HasColumnName("vehicle_id");

        builder.HasIndex(i => i.VehicleId);

        builder.Property(i => i.StorageKey)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("storage_key");

        builder.Property(i => i.IsPrimary)
            .IsRequired()
            .HasColumnName("is_primary");
    }
}
