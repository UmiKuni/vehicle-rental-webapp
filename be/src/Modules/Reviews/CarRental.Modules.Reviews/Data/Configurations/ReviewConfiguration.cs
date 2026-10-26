using CarRental.Modules.Reviews.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Reviews.Data.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id)
            .HasConversion(id => id.Value, val => new ReviewId(val))
            .HasColumnName("id");

        builder.Property(r => r.BookingId)
            .IsRequired()
            .HasColumnName("booking_id");

        builder.HasIndex(r => r.BookingId)
            .IsUnique();

        builder.Property(r => r.VehicleId)
            .IsRequired()
            .HasColumnName("vehicle_id");

        builder.HasIndex(r => r.VehicleId);

        builder.Property(r => r.CustomerId)
            .IsRequired()
            .HasColumnName("customer_id");

        builder.HasIndex(r => r.CustomerId);

        builder.Property(r => r.Rating)
            .IsRequired()
            .HasColumnName("rating");

        builder.Property(r => r.Content)
            .IsRequired()
            .HasMaxLength(2000)
            .HasColumnName("content");

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(r => r.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(r => r.ModerationReason)
            .HasMaxLength(500)
            .HasColumnName("moderation_reason");

        builder.Property(r => r.ModeratedBy)
            .HasColumnName("moderated_by");

        builder.Property(r => r.Version)
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
