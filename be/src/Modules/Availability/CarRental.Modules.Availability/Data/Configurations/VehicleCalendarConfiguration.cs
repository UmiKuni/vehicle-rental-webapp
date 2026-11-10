using CarRental.Modules.Availability.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Availability.Data.Configurations;

public sealed class VehicleCalendarConfiguration : IEntityTypeConfiguration<VehicleCalendar>
{
    public void Configure(EntityTypeBuilder<VehicleCalendar> builder)
    {
        builder.ToTable("vehicle_calendars");

        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id)
            .HasColumnName("vehicle_id");

        builder.Property(c => c.LastSyncedAt)
            .IsRequired()
            .HasColumnName("last_synced_at");

        builder.Property(c => c.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasMany(c => c.Entries)
            .WithOne()
            .HasForeignKey(e => e.VehicleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
