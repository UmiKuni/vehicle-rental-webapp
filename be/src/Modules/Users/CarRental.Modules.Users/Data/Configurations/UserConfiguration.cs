using CarRental.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Users.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, val => UserId.From(val))
            .HasColumnName("id");

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired()
            .HasColumnName("email");

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.Phone)
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("phone");

        builder.HasIndex(u => u.Phone)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .IsRequired()
            .HasColumnName("password_hash");

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("role");

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired()
            .HasColumnName("status");

        builder.Property(u => u.Version)
            .IsRowVersion()
            .HasColumnName("xmin");

        builder.HasOne(u => u.CustomerProfile)
            .WithOne()
            .HasForeignKey<CustomerProfile>(cp => cp.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(u => u.OwnerProfile)
            .WithOne()
            .HasForeignKey<OwnerProfile>(op => op.Id)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(u => u.IdentityDocuments)
            .WithOne()
            .HasForeignKey(d => d.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(u => u.CustomerProfile).AutoInclude();
        builder.Navigation(u => u.OwnerProfile).AutoInclude();
        builder.Navigation(u => u.IdentityDocuments).AutoInclude();
    }
}
