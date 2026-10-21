using CarRental.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Users.Data.Configurations;

public class OwnerProfileConfiguration : IEntityTypeConfiguration<OwnerProfile>
{
    public void Configure(EntityTypeBuilder<OwnerProfile> builder)
    {
        builder.ToTable("owner_profiles");

        builder.HasKey(op => op.Id);
        builder.Property(op => op.Id)
            .HasConversion(id => id.Value, val => UserId.From(val))
            .HasColumnName("user_id");

        builder.Property(op => op.TaxCode)
            .HasMaxLength(64)
            .IsRequired()
            .HasColumnName("tax_code");

        builder.Property(op => op.ApprovalStatus)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("approval_status");

        builder.Property(op => op.RejectReason)
            .HasMaxLength(500)
            .HasColumnName("reject_reason");
    }
}
