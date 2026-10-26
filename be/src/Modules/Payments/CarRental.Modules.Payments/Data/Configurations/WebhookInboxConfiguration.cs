using CarRental.Modules.Payments.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Payments.Data.Configurations;

public sealed class WebhookInboxConfiguration : IEntityTypeConfiguration<WebhookInbox>
{
    public void Configure(EntityTypeBuilder<WebhookInbox> builder)
    {
        builder.ToTable("webhook_inboxes");

        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id)
            .HasMaxLength(128)
            .HasColumnName("id");

        builder.Property(w => w.ProcessedAt)
            .IsRequired()
            .HasColumnName("processed_at");

        builder.Property(w => w.PayloadJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("payload");
    }
}
