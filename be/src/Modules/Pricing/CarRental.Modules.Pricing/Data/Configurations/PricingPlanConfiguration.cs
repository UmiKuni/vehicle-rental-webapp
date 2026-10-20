using CarRental.Modules.Pricing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Pricing.Data.Configurations;

public sealed class PricingPlanConfiguration : IEntityTypeConfiguration<PricingPlan>
{
    public void Configure(EntityTypeBuilder<PricingPlan> builder)
    {
        builder.ToTable("pricing_plans");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.VehicleId)
            .IsRequired()
            .HasColumnName("vehicle_id");

        builder.HasIndex(p => p.VehicleId)
            .IsUnique();

        builder.OwnsOne(p => p.WeekdayRate, nav =>
        {
            nav.Property(m => m.Amount)
                .IsRequired()
                .HasColumnName("weekday_rate_amount");
            nav.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(8)
                .HasColumnName("weekday_rate_currency");
        });

        builder.OwnsOne(p => p.WeekendRate, nav =>
        {
            nav.Property(m => m.Amount)
                .IsRequired()
                .HasColumnName("weekend_rate_amount");
            nav.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(8)
                .HasColumnName("weekend_rate_currency");
        });

        builder.OwnsOne(p => p.DepositAmount, nav =>
        {
            nav.Property(m => m.Amount)
                .IsRequired()
                .HasColumnName("deposit_amount");
            nav.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(8)
                .HasColumnName("deposit_currency");
        });

        builder.Property(p => p.KmLimit)
            .IsRequired()
            .HasColumnName("km_limit");

        builder.Property(p => p.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasMany(p => p.FeeRules)
            .WithOne()
            .HasForeignKey(f => f.PricingPlanId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.FeeRules).AutoInclude();
    }
}
