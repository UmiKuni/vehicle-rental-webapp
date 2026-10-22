using CarRental.Modules.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Payments.Data.Configurations;

public sealed class DepositConfiguration : IEntityTypeConfiguration<Deposit>
{
    public void Configure(EntityTypeBuilder<Deposit> builder)
    {
        builder.ToTable("deposits");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, val => new DepositId(val))
            .HasColumnName("id");

        builder.Property(d => d.BookingId)
            .IsRequired()
            .HasColumnName("booking_id");

        builder.HasIndex(d => d.BookingId);

        builder.OwnsOne(d => d.Amount, nav =>
        {
            nav.Property(m => m.Amount)
                .IsRequired()
                .HasColumnName("amount");
            nav.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(8)
                .HasColumnName("currency");
        });

        builder.OwnsOne(d => d.DeductedAmount, nav =>
        {
            nav.Property(m => m.Amount)
                .IsRequired()
                .HasColumnName("deducted_amount");
            nav.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(8)
                .HasColumnName("deducted_currency");
        });

        builder.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(d => d.ReleaseAfter)
            .HasColumnName("release_after");

        builder.Property(d => d.IsFrozen)
            .IsRequired()
            .HasColumnName("is_frozen");

        builder.Property(d => d.SettledAt)
            .HasColumnName("settled_at");

        builder.Property(d => d.Version)
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
