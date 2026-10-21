using CarRental.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Users.Data.Configurations;

public class IdentityDocumentConfiguration : IEntityTypeConfiguration<IdentityDocument>
{
    public void Configure(EntityTypeBuilder<IdentityDocument> builder)
    {
        builder.ToTable("identity_documents");

        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id)
            .HasColumnName("id");

        builder.Property(d => d.UserId)
            .HasConversion(id => id.Value, val => UserId.From(val))
            .IsRequired()
            .HasColumnName("user_id");

        builder.Property(d => d.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("type");

        builder.Property(d => d.StorageKey)
            .HasMaxLength(500)
            .IsRequired()
            .HasColumnName("storage_key");

        builder.Property(d => d.VerificationStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("verification_status");

        builder.Property(d => d.RejectReason)
            .HasMaxLength(500)
            .HasColumnName("reject_reason");

        builder.HasIndex(d => new { d.UserId, d.Type })
            .IsUnique();
    }
}
