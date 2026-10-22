using HandoverEntity = CarRental.Modules.Handover.Entities.Handover;
using CarRental.Modules.Handover.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Handover.Data;

public class HandoverDbContext : DbContext
{
    public const string Schema = "handover";

    public DbSet<HandoverEntity> Handovers => Set<HandoverEntity>();
    public DbSet<HandoverPhoto> HandoverPhotos => Set<HandoverPhoto>();
    public DbSet<VehicleCondition> VehicleConditions => Set<VehicleCondition>();
    public DbSet<HandoverContext> HandoverContexts => Set<HandoverContext>();

    public HandoverDbContext(DbContextOptions<HandoverDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(HandoverDbContext).Assembly);
    }
}
