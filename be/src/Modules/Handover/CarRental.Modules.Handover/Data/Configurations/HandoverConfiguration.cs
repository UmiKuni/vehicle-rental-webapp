using HandoverEntity = CarRental.Modules.Handover.Entities.Handover;
using CarRental.Modules.Handover.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Handover.Data.Configurations;

public sealed class HandoverConfiguration : IEntityTypeConfiguration<HandoverEntity>
{
    public void Configure(EntityTypeBuilder<HandoverEntity> builder)
    {
        builder.ToTable("handovers");

        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id)
            .HasConversion(id => id.Value, val => new HandoverId(val))
            .HasColumnName("id");

        builder.Property(h => h.BookingId)
            .IsRequired()
            .HasColumnName("booking_id");

        builder.Property(h => h.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("type");

        builder.HasIndex(h => new { h.BookingId, h.Type })
            .IsUnique();

        builder.Property(h => h.ServerReceivedAt)
            .IsRequired()
            .HasColumnName("server_received_at");

        builder.Property(h => h.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(h => h.ConfirmedByCustomerAt)
            .HasColumnName("confirmed_by_customer_at");

        builder.Property(h => h.ConfirmedByOwnerAt)
            .HasColumnName("confirmed_by_owner_at");

        builder.Property(h => h.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasMany(h => h.Photos)
            .WithOne()
            .HasForeignKey(p => p.HandoverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(h => h.Condition)
            .WithOne()
            .HasForeignKey<VehicleCondition>(c => c.HandoverId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(h => h.Photos).AutoInclude();
        builder.Navigation(h => h.Condition).AutoInclude();
    }
}
