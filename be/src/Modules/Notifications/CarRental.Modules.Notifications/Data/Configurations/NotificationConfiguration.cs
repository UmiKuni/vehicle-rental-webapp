using CarRental.Modules.Notifications.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Notifications.Data.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id)
            .HasConversion(id => id.Value, val => new NotificationId(val))
            .HasColumnName("id");

        builder.Property(n => n.UserId)
            .IsRequired()
            .HasColumnName("user_id");

        builder.HasIndex(n => n.UserId);

        builder.Property(n => n.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("type");

        builder.Property(n => n.Content)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasColumnName("content");

        builder.Property(n => n.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(n => n.Attempts)
            .IsRequired()
            .HasColumnName("attempts");

        builder.Property(n => n.LastError)
            .HasMaxLength(1000)
            .HasColumnName("last_error");

        builder.Property(n => n.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");

        builder.Property(n => n.SentAt)
            .HasColumnName("sent_at");

        builder.Property(n => n.DedupKey)
            .IsRequired()
            .HasMaxLength(256)
            .HasColumnName("dedup_key");

        builder.HasIndex(n => n.DedupKey)
            .IsUnique();

        builder.Property(n => n.Version)
            .IsRowVersion()
            .HasColumnName("xmin");
    }
}
