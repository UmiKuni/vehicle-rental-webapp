using CarRental.Modules.Bookings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Bookings.Data.Configurations;

public sealed class BookingStatusHistoryConfiguration : IEntityTypeConfiguration<BookingStatusHistory>
{
    public void Configure(EntityTypeBuilder<BookingStatusHistory> builder)
    {
        builder.ToTable("booking_status_histories");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasColumnName("id");

        builder.Property(h => h.BookingId)
            .HasConversion(id => id.Value, val => new BookingId(val))
            .IsRequired()
            .HasColumnName("booking_id");

        builder.HasIndex(h => h.BookingId);

        builder.Property(h => h.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(h => h.ChangedAt)
            .IsRequired()
            .HasColumnName("changed_at");

        builder.Property(h => h.Reason)
            .HasMaxLength(500)
            .HasColumnName("reason");
    }
}
