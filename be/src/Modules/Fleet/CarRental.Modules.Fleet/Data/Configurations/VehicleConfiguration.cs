using CarRental.Modules.Fleet.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Fleet.Data.Configurations;

public class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("vehicles");

        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id)
            .HasConversion(id => id.Value, val => new VehicleId(val))
            .HasColumnName("id");

        builder.Property(v => v.OwnerId)
            .IsRequired()
            .HasColumnName("owner_id");

        builder.HasIndex(v => v.OwnerId);

        builder.Property(v => v.PlateNumber)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnName("plate_number");

        builder.HasIndex(v => v.PlateNumber)
            .IsUnique();

        builder.Property(v => v.BrandId)
            .IsRequired()
            .HasColumnName("brand_id");

        builder.Property(v => v.ModelId)
            .IsRequired()
            .HasColumnName("model_id");

        builder.Property(v => v.Transmission)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("transmission");

        builder.Property(v => v.Seats)
            .IsRequired()
            .HasColumnName("seats");

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.OwnsOne(v => v.Location, loc =>
        {
            loc.Property(p => p.Latitude)
                .IsRequired()
                .HasColumnName("location_lat");
            loc.Property(p => p.Longitude)
                .IsRequired()
                .HasColumnName("location_lon");
        });

        builder.Property(v => v.Address)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("address");

        builder.Property(v => v.HiddenByAdmin)
            .IsRequired()
            .HasColumnName("hidden_by_admin");

        builder.Property(v => v.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasMany(v => v.Images)
            .WithOne()
            .HasForeignKey(i => i.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(v => v.Approvals)
            .WithOne()
            .HasForeignKey(a => a.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(v => v.Images).AutoInclude();
    }
}
