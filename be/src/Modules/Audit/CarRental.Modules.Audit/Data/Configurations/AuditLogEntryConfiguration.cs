using CarRental.Modules.Audit.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Audit.Data.Configurations;

public class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> builder)
    {
        builder.ToTable("audit_log_entries", AuditDbContext.Schema);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasColumnName("id");

        builder.Property(x => x.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(x => x.ActorId)
            .HasColumnName("actor_id")
            .IsRequired();

        builder.Property(x => x.IpAddress)
            .HasColumnName("ip_address")
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(x => x.Action)
            .HasColumnName("action")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.EntityName)
            .HasColumnName("entity_name")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.EntityId)
            .HasColumnName("entity_id")
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(x => x.BeforeState)
            .HasColumnName("before_state")
            .HasColumnType("jsonb");

        builder.Property(x => x.AfterState)
            .HasColumnName("after_state")
            .HasColumnType("jsonb");

        builder.HasIndex(x => x.Timestamp);
        builder.HasIndex(x => x.ActorId);
        builder.HasIndex(x => new { x.EntityName, x.EntityId });
    }
}
