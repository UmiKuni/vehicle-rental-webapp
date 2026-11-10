using CarRental.Modules.Bookings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Bookings.Data.Configurations;

public sealed class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");

        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id)
            .HasConversion(id => id.Value, val => new BookingId(val))
            .HasColumnName("id");

        builder.Property(b => b.CustomerId)
            .IsRequired()
            .HasColumnName("customer_id");

        builder.HasIndex(b => b.CustomerId);

        builder.Property(b => b.OwnerId)
            .IsRequired()
            .HasColumnName("owner_id");

        builder.HasIndex(b => b.OwnerId);

        builder.Property(b => b.VehicleId)
            .IsRequired()
            .HasColumnName("vehicle_id");

        builder.HasIndex(b => b.VehicleId);

        builder.Property(b => b.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(b => b.StartTime)
            .IsRequired()
            .HasColumnName("start_time");

        builder.Property(b => b.EndTime)
            .IsRequired()
            .HasColumnName("end_time");

        builder.Property(b => b.PickupLocation)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("pickup_location");

        builder.Property(b => b.PaymentDeadline)
            .HasColumnName("payment_deadline");

        builder.Property(b => b.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(b => b.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        builder.Property(b => b.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasMany(b => b.LineItems)
            .WithOne()
            .HasForeignKey(l => l.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(b => b.PolicySnapshot)
            .WithOne()
            .HasForeignKey<CancellationPolicySnapshot>(p => p.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(b => b.StatusHistory)
            .WithOne()
            .HasForeignKey(h => h.BookingId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(b => b.LineItems).AutoInclude();
        builder.Navigation(b => b.PolicySnapshot).AutoInclude();
        builder.Navigation(b => b.StatusHistory).AutoInclude();
    }
}
