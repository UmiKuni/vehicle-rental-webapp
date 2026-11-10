using CarRental.Modules.Reviews.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Reviews.Data.Configurations;

public sealed class ReviewableBookingConfiguration : IEntityTypeConfiguration<ReviewableBooking>
{
    public void Configure(EntityTypeBuilder<ReviewableBooking> builder)
    {
        builder.ToTable("reviewable_bookings");

        builder.HasKey(b => b.BookingId);
        builder.Property(b => b.BookingId)
            .HasColumnName("booking_id");

        builder.Ignore(b => b.Id);

        builder.Property(b => b.VehicleId)
            .IsRequired()
            .HasColumnName("vehicle_id");

        builder.Property(b => b.CustomerId)
            .IsRequired()
            .HasColumnName("customer_id");

        builder.Property(b => b.CompletedAt)
            .IsRequired()
            .HasColumnName("completed_at");

        builder.Property(b => b.HasReviewed)
            .IsRequired()
            .HasColumnName("has_reviewed");
    }
}
