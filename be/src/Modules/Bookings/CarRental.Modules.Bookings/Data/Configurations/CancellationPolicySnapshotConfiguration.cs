using CarRental.Modules.Bookings.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Bookings.Data.Configurations;

public sealed class CancellationPolicySnapshotConfiguration : IEntityTypeConfiguration<CancellationPolicySnapshot>
{
    public void Configure(EntityTypeBuilder<CancellationPolicySnapshot> builder)
    {
        builder.ToTable("cancellation_policy_snapshots");

        builder.HasKey(p => p.BookingId);
        builder.Property(p => p.BookingId)
            .HasColumnName("booking_id");

        builder.Ignore(p => p.Id);

        builder.Property(p => p.PolicyDetailsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("policy_details");
    }
}
