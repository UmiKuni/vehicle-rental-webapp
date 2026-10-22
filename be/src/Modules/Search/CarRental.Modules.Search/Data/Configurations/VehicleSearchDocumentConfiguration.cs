using CarRental.Modules.Search.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Search.Data.Configurations;

public sealed class VehicleSearchDocumentConfiguration : IEntityTypeConfiguration<VehicleSearchDocument>
{
    public void Configure(EntityTypeBuilder<VehicleSearchDocument> builder)
    {
        builder.ToTable("vehicle_search_documents");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("vehicle_id");

        builder.OwnsOne(d => d.Location, loc =>
        {
            loc.Property(p => p.Latitude)
                .IsRequired()
                .HasColumnName("location_lat");
            loc.Property(p => p.Longitude)
                .IsRequired()
                .HasColumnName("location_lon");
        });

        builder.Property(d => d.Brand)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("brand");

        builder.Property(d => d.Model)
            .IsRequired()
            .HasMaxLength(100)
            .HasColumnName("model");

        builder.Property(d => d.Seats)
            .IsRequired()
            .HasColumnName("seats");

        builder.Property(d => d.Transmission)
            .IsRequired()
            .HasMaxLength(32)
            .HasColumnName("transmission");

        builder.Property(d => d.Address)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("address");

        builder.Property(d => d.PriceFrom)
            .HasPrecision(18, 2)
            .HasColumnName("price_from");

        builder.Property(d => d.AverageRating)
            .HasPrecision(3, 2)
            .HasColumnName("average_rating");

        builder.Property(d => d.TotalReviews)
            .IsRequired()
            .HasColumnName("total_reviews");

        builder.Property(d => d.IsPublished)
            .IsRequired()
            .HasColumnName("is_published");

        builder.Property(d => d.LastFleetEventAt)
            .HasColumnName("last_fleet_event_at");

        builder.Property(d => d.LastPricingEventAt)
            .HasColumnName("last_pricing_event_at");

        builder.Property(d => d.LastRatingEventAt)
            .HasColumnName("last_rating_event_at");

        builder.Property(d => d.Version)
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
