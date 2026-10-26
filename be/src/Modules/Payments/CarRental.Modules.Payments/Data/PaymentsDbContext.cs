using CarRental.Modules.Payments.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Payments.Data;

public class PaymentsDbContext : DbContext
{
    public const string Schema = "payments";

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Deposit> Deposits => Set<Deposit>();
    public DbSet<WebhookInbox> WebhookInboxes => Set<WebhookInbox>();

    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PaymentsDbContext).Assembly);
    }
}
