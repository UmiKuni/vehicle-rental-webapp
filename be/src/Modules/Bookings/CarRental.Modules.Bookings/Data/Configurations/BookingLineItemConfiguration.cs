using CarRental.Modules.Bookings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Bookings.Data.Configurations;

public sealed class BookingLineItemConfiguration : IEntityTypeConfiguration<BookingLineItem>
{
    public void Configure(EntityTypeBuilder<BookingLineItem> builder)
    {
        builder.ToTable("booking_line_items");

        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id)
            .HasColumnName("id");

        builder.Property(l => l.BookingId)
            .IsRequired()
            .HasColumnName("booking_id");

        builder.Property(l => l.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("type");

        builder.OwnsOne(l => l.Amount, nav =>
        {
            nav.Property(m => m.Amount)
                .IsRequired()
                .HasColumnName("amount");
            nav.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(8)
                .HasColumnName("currency");
        });
    }
}
