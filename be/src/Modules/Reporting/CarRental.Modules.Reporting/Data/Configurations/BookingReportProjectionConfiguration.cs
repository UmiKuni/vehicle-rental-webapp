using CarRental.Modules.Reporting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Reporting.Data.Configurations;

internal sealed class BookingReportProjectionConfiguration : IEntityTypeConfiguration<BookingReportProjection>
{
    public void Configure(EntityTypeBuilder<BookingReportProjection> builder)
    {
        builder.ToTable("booking_report_projections", ReportingDbContext.Schema);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("booking_id")
            .ValueGeneratedNever();

        builder.Property(x => x.OwnerId)
            .HasColumnName("owner_id")
            .IsRequired();

        builder.Property(x => x.VehicleId)
            .HasColumnName("vehicle_id")
            .IsRequired();

        builder.Property(x => x.VehiclePlate)
            .HasColumnName("vehicle_plate")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.VehicleModel)
            .HasColumnName("vehicle_model")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.StartTime)
            .HasColumnName("start_time")
            .IsRequired();

        builder.Property(x => x.EndTime)
            .HasColumnName("end_time")
            .IsRequired();

        builder.Property(x => x.RentAmount)
            .HasColumnName("rent_amount")
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(x => x.PaidAmount)
            .HasColumnName("paid_amount")
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.RefundedAmount)
            .HasColumnName("refunded_amount")
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.Status)
            .HasColumnName("status")
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(x => x.PaidAt)
            .HasColumnName("paid_at");

        builder.Property(x => x.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(x => x.CancelledAt)
            .HasColumnName("cancelled_at");

        builder.HasIndex(x => x.OwnerId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => new { x.OwnerId, x.StartTime });
    }
}
