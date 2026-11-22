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

    public async Task<(IReadOnlyList<AuditLogEntry> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Guid? actorId = null,
        string? entityName = null,
        string? entityId = null,
        string? action = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.AuditLogEntries.AsNoTracking();

        if (actorId.HasValue && actorId.Value != Guid.Empty)
        {
            query = query.Where(entry => entry.ActorId == actorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(entityName))
        {
            query = query.Where(entry => entry.EntityName == entityName.Trim());
        }

        if (!string.IsNullOrWhiteSpace(entityId))
        {
            query = query.Where(entry => entry.EntityId == entityId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            query = query.Where(entry => entry.Action == action.Trim());
        }

        if (from.HasValue)
        {
            query = query.Where(entry => entry.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(entry => entry.Timestamp <= to.Value);
        }

        var totalCount = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(entry => entry.Timestamp)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(ct);

        return (items, totalCount);
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
