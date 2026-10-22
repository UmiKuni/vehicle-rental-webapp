using CarRental.Modules.Audit.Entities;
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
}
