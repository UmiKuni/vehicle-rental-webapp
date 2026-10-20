using CarRental.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Users.Data.Configurations;

public class CustomerProfileConfiguration : IEntityTypeConfiguration<CustomerProfile>
{
    public void Configure(EntityTypeBuilder<CustomerProfile> builder)
    {
        builder.ToTable("customer_profiles");

        builder.HasKey(cp => cp.Id);
        builder.Property(cp => cp.Id)
            .HasColumnName("user_id");

        builder.Property(cp => cp.FullName)
            .HasMaxLength(256)
            .IsRequired()
            .HasColumnName("full_name");

        builder.Property(cp => cp.Address)
            .HasMaxLength(500)
            .IsRequired()
            .HasColumnName("address");
    }
}
