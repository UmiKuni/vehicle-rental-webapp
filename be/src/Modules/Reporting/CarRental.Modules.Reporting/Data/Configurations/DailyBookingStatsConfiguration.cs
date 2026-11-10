using CarRental.Modules.Reporting.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Reporting.Data.Configurations;

public class DailyBookingStatsConfiguration : IEntityTypeConfiguration<DailyBookingStats>
{
    public void Configure(EntityTypeBuilder<DailyBookingStats> builder)
    {
        builder.ToTable("daily_booking_stats", ReportingDbContext.Schema);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("date")
            .ValueGeneratedNever();

        builder.Ignore(x => x.Date);

        builder.Property(x => x.TotalBookings)
            .HasColumnName("total_bookings")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(x => x.TotalRevenue)
            .HasColumnName("total_revenue")
            .HasPrecision(18, 2)
            .HasDefaultValue(0m)
            .IsRequired();

        builder.Property(x => x.CancellationCount)
            .HasColumnName("cancellation_count")
            .HasDefaultValue(0)
            .IsRequired();
    }
}
