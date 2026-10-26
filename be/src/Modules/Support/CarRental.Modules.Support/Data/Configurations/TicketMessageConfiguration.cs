using CarRental.Modules.Support.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Support.Data.Configurations;

public sealed class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("ticket_messages");

        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id)
            .HasColumnName("id");

        builder.Property(m => m.SupportTicketId)
            .HasConversion(id => id.Value, val => new TicketId(val))
            .IsRequired()
            .HasColumnName("support_ticket_id");

        builder.HasIndex(m => m.SupportTicketId);

        builder.Property(m => m.SenderId)
            .IsRequired()
            .HasColumnName("sender_id");

        builder.Property(m => m.Content)
            .IsRequired()
            .HasMaxLength(4000)
            .HasColumnName("content");

        builder.Property(m => m.AttachmentKey)
            .HasMaxLength(500)
            .HasColumnName("attachment_key");

        builder.Property(m => m.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");
    }
}
