using CarRental.Modules.Handover.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Handover.Data.Configurations;

public sealed class HandoverContextConfiguration : IEntityTypeConfiguration<HandoverContext>
{
    public void Configure(EntityTypeBuilder<HandoverContext> builder)
    {
        builder.ToTable("handover_contexts");

        builder.HasKey(c => c.BookingId);
        builder.Property(c => c.BookingId)
            .HasColumnName("booking_id");

        builder.Ignore(c => c.Id);

        builder.Property(c => c.VehicleId)
            .IsRequired()
            .HasColumnName("vehicle_id");

        builder.Property(c => c.CustomerId)
            .IsRequired()
            .HasColumnName("customer_id");

        builder.Property(c => c.OwnerId)
            .IsRequired()
            .HasColumnName("owner_id");

        builder.Property(c => c.StartTime)
            .IsRequired()
            .HasColumnName("start_time");

        builder.Property(c => c.EndTime)
            .IsRequired()
            .HasColumnName("end_time");

        builder.Property(c => c.IsActive)
            .IsRequired()
            .HasColumnName("is_active");
    }
}
