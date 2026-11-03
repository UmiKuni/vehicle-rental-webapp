using CarRental.Modules.Pricing.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Pricing.Data.Configurations;

public sealed class FeeRuleConfiguration : IEntityTypeConfiguration<FeeRule>
{
    public void Configure(EntityTypeBuilder<FeeRule> builder)
    {
        builder.ToTable("fee_rules");

        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id)
            .HasColumnName("id");

        builder.Property(f => f.PricingPlanId)
            .IsRequired()
            .HasColumnName("pricing_plan_id");

        builder.Property(f => f.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("type");

        builder.HasIndex(f => new { f.PricingPlanId, f.Type })
            .IsUnique();

        builder.OwnsOne(f => f.Amount, nav =>
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
