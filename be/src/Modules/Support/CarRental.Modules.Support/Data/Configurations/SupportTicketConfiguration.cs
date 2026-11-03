using CarRental.Modules.Support.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Support.Data.Configurations;

public sealed class SupportTicketConfiguration : IEntityTypeConfiguration<SupportTicket>
{
    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("support_tickets");

        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, val => new TicketId(val))
            .HasColumnName("id");

        builder.Property(t => t.BookingId)
            .IsRequired()
            .HasColumnName("booking_id");

        builder.HasIndex(t => t.BookingId);

        builder.Property(t => t.ReporterId)
            .IsRequired()
            .HasColumnName("reporter_id");

        builder.HasIndex(t => t.ReporterId);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(t => t.DepositToCustomerPercent)
            .HasColumnName("deposit_to_customer_percent");

        builder.Property(t => t.ResolutionNote)
            .HasMaxLength(2000)
            .HasColumnName("resolution_note");

        builder.Property(t => t.ResolvedBy)
            .HasColumnName("resolved_by");

        builder.Property(t => t.ResolvedAt)
            .HasColumnName("resolved_at");

        builder.Property(t => t.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(t => t.UpdatedAt)
            .IsRequired()
            .HasColumnName("updated_at");

        builder.Property(t => t.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasMany(t => t.Messages)
            .WithOne()
            .HasForeignKey(m => m.SupportTicketId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(t => t.Messages).AutoInclude();
    }
}
