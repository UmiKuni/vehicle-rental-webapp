using CarRental.Modules.Search.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Search.Data;

public class SearchDbContext : DbContext
{
    public const string Schema = "search";

    public DbSet<VehicleSearchDocument> VehicleSearchDocuments => Set<VehicleSearchDocument>();

    public SearchDbContext(DbContextOptions<SearchDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SearchDbContext).Assembly);
    }
}
