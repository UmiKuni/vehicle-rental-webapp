using CarRental.Modules.Availability.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Availability.Data.Configurations;

public sealed class CalendarEntryConfiguration : IEntityTypeConfiguration<CalendarEntry>
{
    public void Configure(EntityTypeBuilder<CalendarEntry> builder)
    {
        builder.ToTable("calendar_entries");

        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id)
            .HasColumnName("id");

        builder.Property(e => e.VehicleId)
            .IsRequired()
            .HasColumnName("vehicle_id");

        builder.HasIndex(e => e.VehicleId);

        builder.Property(e => e.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("type");

        builder.OwnsOne(e => e.TimeRange, r =>
        {
            r.Property(p => p.Start)
                .IsRequired()
                .HasColumnName("range_start");
            r.Property(p => p.End)
                .IsRequired()
                .HasColumnName("range_end");
        });

        builder.Property(e => e.ExpiresAt)
            .HasColumnName("expires_at");

        builder.Property(e => e.BookingId)
            .HasColumnName("booking_id");

        builder.HasIndex(e => e.BookingId);
    }
}
