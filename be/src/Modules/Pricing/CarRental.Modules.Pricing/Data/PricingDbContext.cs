using CarRental.Modules.Pricing.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Pricing.Data;

public class PricingDbContext : DbContext
{
    public const string Schema = "pricing";

    public DbSet<PricingPlan> PricingPlans => Set<PricingPlan>();
    public DbSet<FeeRule> FeeRules => Set<FeeRule>();
    public DbSet<PricingPlanVersion> PricingPlanVersions => Set<PricingPlanVersion>();

    public PricingDbContext(DbContextOptions<PricingDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PricingDbContext).Assembly);
    }
}
