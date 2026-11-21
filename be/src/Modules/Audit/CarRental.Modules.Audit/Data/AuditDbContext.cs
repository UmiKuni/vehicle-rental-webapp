using CarRental.Modules.Audit.Entities;
using CarRental.BuildingBlocks.Results;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Audit.Data;

public class AuditDbContext : DbContext
{
    public const string Schema = "audit";

    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();

    public AuditDbContext(DbContextOptions<AuditDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuditDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureAppendOnly();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        EnsureAppendOnly();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void EnsureAppendOnly()
    {
        if (ChangeTracker.Entries<AuditLogEntry>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted))
        {
            throw new DomainException(
                "audit.append_only",
                "Audit log entries cannot be modified or deleted.");
        }
    }
}
