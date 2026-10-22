using CarRental.Modules.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Payments.Data.Configurations;

public sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, val => new PaymentId(val))
            .HasColumnName("id");

        builder.Property(p => p.BookingId)
            .IsRequired()
            .HasColumnName("booking_id");

        builder.HasIndex(p => p.BookingId);

        builder.OwnsOne(p => p.Amount, nav =>
        {
            nav.Property(m => m.Amount)
                .IsRequired()
                .HasColumnName("amount");
            nav.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(8)
                .HasColumnName("currency");
        });

        builder.OwnsOne(p => p.DepositPortion, nav =>
        {
            nav.Property(m => m.Amount)
                .IsRequired()
                .HasColumnName("deposit_portion_amount");
            nav.Property(m => m.Currency)
                .IsRequired()
                .HasMaxLength(8)
                .HasColumnName("deposit_portion_currency");
        });

        builder.Property(p => p.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(p => p.GatewayTransactionId)
            .HasMaxLength(128)
            .HasColumnName("gateway_transaction_id");

        builder.HasIndex(p => p.GatewayTransactionId)
            .IsUnique()
            .HasFilter("gateway_transaction_id IS NOT NULL");

        builder.Property(p => p.Method)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("method");

        builder.Property(p => p.FailureReason)
            .HasMaxLength(500)
            .HasColumnName("failure_reason");

        builder.Property(p => p.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(p => p.CompletedAt)
            .HasColumnName("completed_at");

        builder.Property(p => p.Version)
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
