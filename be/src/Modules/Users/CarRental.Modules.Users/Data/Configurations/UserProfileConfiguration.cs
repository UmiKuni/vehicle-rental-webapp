using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.Modules.Users.Entities;
using CarRental.Modules.Users.Enums;
using CarRental.Modules.Users.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CarRental.Modules.Users.Data.Configurations;

public class UserProfileConfiguration : IEntityTypeConfiguration<UserProfile>
{
    public void Configure(EntityTypeBuilder<UserProfile> builder)
    {
        builder.ToTable("user_profiles");

        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasConversion(id => id.Value, value => new UserId(value))
            .IsRequired();

        builder.Property(u => u.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(u => u.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.DateOfBirth);

        builder.Property(u => u.Version)
            .IsRowVersion();

        // Map Roles HashSet via backing field
        builder.Property(u => u.Roles)
            .HasField("_roles")
            .HasConversion(
                roles => string.Join(",", roles.Select(r => r.ToString())),
                dbValue => dbValue
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(Enum.Parse<UserRole>)
                    .ToHashSet())
            .HasMaxLength(100);

        // Value Object: PhoneNumber
        builder.OwnsOne(u => u.PhoneNumber, pb =>
        {
            pb.Property(p => p.E164)
                .HasColumnName("phone_number")
                .HasMaxLength(30);
        });

        // Value Object: Avatar FileReference
        builder.OwnsOne(u => u.Avatar, ab =>
        {
            ab.Property(a => a.StorageKey).HasColumnName("avatar_storage_key").HasMaxLength(256);
            ab.Property(a => a.Url).HasColumnName("avatar_url").HasMaxLength(1024);
            ab.Property(a => a.IsPrivate).HasColumnName("avatar_is_private");
            ab.Property(a => a.ContentType).HasColumnName("avatar_content_type").HasMaxLength(100);
            ab.Property(a => a.SizeBytes).HasColumnName("avatar_size_bytes");
        });

        // Value Object: PayoutAccount
        builder.OwnsOne(u => u.PayoutAccount, pob =>
        {
            pob.Property(p => p.BankName).HasColumnName("payout_bank_name").HasMaxLength(100);
            pob.Property(p => p.AccountNumber).HasColumnName("payout_account_number").HasMaxLength(50);
            pob.Property(p => p.AccountHolderName).HasColumnName("payout_account_holder").HasMaxLength(150);
        });

        // Child Entity: IdentityDocument
        builder.OwnsOne(u => u.IdentityDocument, idb =>
        {
            idb.ToTable("identity_documents");
            idb.WithOwner().HasForeignKey("UserId");
            idb.HasKey("Id");

            idb.Property(i => i.CitizenIdNumber)
                .IsRequired()
                .HasMaxLength(50);

            idb.Property(i => i.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            idb.Property(i => i.RejectReason)
                .HasMaxLength(500);

            idb.OwnsOne(i => i.FrontImage, fb =>
            {
                fb.Property(f => f.StorageKey).HasColumnName("front_storage_key").HasMaxLength(256);
                fb.Property(f => f.Url).HasColumnName("front_url").HasMaxLength(1024);
                fb.Property(f => f.IsPrivate).HasColumnName("front_is_private");
                fb.Property(f => f.ContentType).HasColumnName("front_content_type").HasMaxLength(100);
                fb.Property(f => f.SizeBytes).HasColumnName("front_size_bytes");
            });

            idb.OwnsOne(i => i.BackImage, bb =>
            {
                bb.Property(b => b.StorageKey).HasColumnName("back_storage_key").HasMaxLength(256);
                bb.Property(b => b.Url).HasColumnName("back_url").HasMaxLength(1024);
                bb.Property(b => b.IsPrivate).HasColumnName("back_is_private");
                bb.Property(b => b.ContentType).HasColumnName("back_content_type").HasMaxLength(100);
                bb.Property(b => b.SizeBytes).HasColumnName("back_size_bytes");
            });

            idb.OwnsOne(i => i.SelfieImage, sb =>
            {
                sb.Property(s => s.StorageKey).HasColumnName("selfie_storage_key").HasMaxLength(256);
                sb.Property(s => s.Url).HasColumnName("selfie_url").HasMaxLength(1024);
                sb.Property(s => s.IsPrivate).HasColumnName("selfie_is_private");
                sb.Property(s => s.ContentType).HasColumnName("selfie_content_type").HasMaxLength(100);
                sb.Property(s => s.SizeBytes).HasColumnName("selfie_size_bytes");
            });
        });

        // Child Entity: DriverLicense
        builder.OwnsOne(u => u.DriverLicense, dlb =>
        {
            dlb.ToTable("driver_licenses");
            dlb.WithOwner().HasForeignKey("UserId");
            dlb.HasKey("Id");

            dlb.Property(d => d.Number)
                .IsRequired()
                .HasMaxLength(50);

            dlb.Property(d => d.Class)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            dlb.Property(d => d.Status)
                .HasConversion<string>()
                .HasMaxLength(50)
                .IsRequired();

            dlb.Property(d => d.RejectReason)
                .HasMaxLength(500);

            dlb.OwnsOne(d => d.Image, ib =>
            {
                ib.Property(img => img.StorageKey).HasColumnName("image_storage_key").HasMaxLength(256);
                ib.Property(img => img.Url).HasColumnName("image_url").HasMaxLength(1024);
                ib.Property(img => img.IsPrivate).HasColumnName("image_is_private");
                ib.Property(img => img.ContentType).HasColumnName("image_content_type").HasMaxLength(100);
                ib.Property(img => img.SizeBytes).HasColumnName("image_size_bytes");
            });
        });

        builder.Navigation(u => u.IdentityDocument).IsRequired(false);
        builder.Navigation(u => u.DriverLicense).IsRequired(false);
    }
}
