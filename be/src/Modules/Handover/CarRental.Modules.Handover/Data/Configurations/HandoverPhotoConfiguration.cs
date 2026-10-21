using CarRental.Modules.Handover.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Handover.Data.Configurations;

public sealed class HandoverPhotoConfiguration : IEntityTypeConfiguration<HandoverPhoto>
{
    public void Configure(EntityTypeBuilder<HandoverPhoto> builder)
    {
        builder.ToTable("handover_photos");

        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id)
            .HasColumnName("id");

        builder.Property(p => p.HandoverId)
            .IsRequired()
            .HasColumnName("handover_id");

        builder.Property(p => p.Corner)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("corner");

        builder.Property(p => p.StorageKey)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnName("storage_key");

        builder.Property(p => p.FileHash)
            .IsRequired()
            .HasMaxLength(128)
            .HasColumnName("file_hash");

        builder.Property(p => p.ClientCapturedAt)
            .IsRequired()
            .HasColumnName("client_captured_at");
    }
}
