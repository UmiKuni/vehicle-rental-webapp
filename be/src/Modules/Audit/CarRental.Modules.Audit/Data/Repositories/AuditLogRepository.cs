using CarRental.Modules.Audit.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Audit.Data.Repositories;

internal sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AuditDbContext _dbContext;

    public AuditLogRepository(AuditDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(AuditLogEntry entry, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        await _dbContext.AuditLogEntries.AddAsync(entry, ct);
    }

    public Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        _dbContext.AuditLogEntries
            .AsNoTracking()
            .SingleOrDefaultAsync(entry => entry.Id == id, ct);

    public async Task<IReadOnlyList<AuditLogEntry>> GetByEntityAsync(
        string entityName,
        string entityId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(entityName))
            throw new ArgumentException("Entity name must not be empty.", nameof(entityName));
        if (string.IsNullOrWhiteSpace(entityId))
            throw new ArgumentException("Entity ID must not be empty.", nameof(entityId));

        return await _dbContext.AuditLogEntries
            .AsNoTracking()
            .Where(entry => entry.EntityName == entityName && entry.EntityId == entityId)
            .OrderByDescending(entry => entry.Timestamp)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AuditLogEntry>> GetByActorAsync(
        Guid actorId,
        CancellationToken ct = default)
    {
        if (actorId == Guid.Empty)
            throw new ArgumentException("Actor ID must not be empty.", nameof(actorId));

        return await _dbContext.AuditLogEntries
            .AsNoTracking()
            .Where(entry => entry.ActorId == actorId)
            .OrderByDescending(entry => entry.Timestamp)
            .ToListAsync(ct);
    }
}
