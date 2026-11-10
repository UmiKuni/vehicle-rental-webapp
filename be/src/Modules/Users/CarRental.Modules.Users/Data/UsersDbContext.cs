using CarRental.Modules.Users.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Users.Data;

public class UsersDbContext : DbContext
{
    public const string Schema = "users";

    public DbSet<User> Users => Set<User>();
    public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();
    public DbSet<OwnerProfile> OwnerProfiles => Set<OwnerProfile>();
    public DbSet<IdentityDocument> IdentityDocuments => Set<IdentityDocument>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(UsersDbContext).Assembly);
    }
}
