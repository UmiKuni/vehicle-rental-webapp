using CarRental.Modules.Fleet.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Fleet.Data.Configurations;

public class VehicleApprovalConfiguration : IEntityTypeConfiguration<VehicleApproval>
{
    public void Configure(EntityTypeBuilder<VehicleApproval> builder)
    {
        builder.ToTable("vehicle_approvals");

        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id)
            .HasColumnName("id");

        builder.Property(a => a.VehicleId)
            .IsRequired()
            .HasColumnName("vehicle_id");

        builder.HasIndex(a => a.VehicleId);

        builder.Property(a => a.AdminId)
            .IsRequired()
            .HasColumnName("admin_id");

        builder.Property(a => a.Decision)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("decision");

        builder.Property(a => a.Reason)
            .HasMaxLength(1000)
            .HasColumnName("reason");

        builder.Property(a => a.CreatedAt)
            .IsRequired()
            .HasColumnName("created_at");
    }
}
