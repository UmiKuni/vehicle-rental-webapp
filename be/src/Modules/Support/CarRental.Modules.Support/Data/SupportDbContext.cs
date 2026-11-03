using CarRental.Modules.Support.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Support.Data;

public class SupportDbContext : DbContext
{
    public const string Schema = "support";

    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

    public SupportDbContext(DbContextOptions<SupportDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SupportDbContext).Assembly);
    }
}
