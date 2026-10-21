using CarRental.Modules.Handover.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Handover.Data.Configurations;

public sealed class VehicleConditionConfiguration : IEntityTypeConfiguration<VehicleCondition>
{
    public void Configure(EntityTypeBuilder<VehicleCondition> builder)
    {
        builder.ToTable("vehicle_conditions");

        builder.HasKey(c => c.HandoverId);
        builder.Property(c => c.HandoverId)
            .HasConversion(id => id.Value, val => new HandoverId(val))
            .HasColumnName("handover_id");

        builder.Ignore(c => c.Id);

        builder.Property(c => c.Odometer)
            .IsRequired()
            .HasColumnName("odometer");

        builder.Property(c => c.FuelLevel)
            .IsRequired()
            .HasColumnName("fuel_level");

        builder.Property(c => c.DamageNotes)
            .HasMaxLength(1000)
            .HasColumnName("damage_notes");
    }
}
