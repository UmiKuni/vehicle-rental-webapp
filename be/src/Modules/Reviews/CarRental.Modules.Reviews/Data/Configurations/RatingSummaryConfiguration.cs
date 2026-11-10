using CarRental.Modules.Reviews.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Reviews.Data.Configurations;

public sealed class RatingSummaryConfiguration : IEntityTypeConfiguration<RatingSummary>
{
    public void Configure(EntityTypeBuilder<RatingSummary> builder)
    {
        builder.ToTable("rating_summaries");

        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasColumnName("vehicle_id");

        builder.Property(s => s.AverageRating)
            .HasPrecision(3, 2)
            .IsRequired()
            .HasColumnName("average_rating");

        builder.Property(s => s.TotalReviews)
            .IsRequired()
            .HasColumnName("total_reviews");

        builder.Property(s => s.Version)
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
