using CarRental.Modules.Audit.Entities;

namespace CarRental.Modules.Audit.Data.Repositories;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLogEntry entry, CancellationToken ct = default);

    Task<(IReadOnlyList<AuditLogEntry> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        Guid? actorId = null,
        string? entityName = null,
        string? entityId = null,
        string? action = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);

    Task<AuditLogEntry?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> GetByEntityAsync(
        string entityName,
        string entityId,
        CancellationToken ct = default);

    Task<IReadOnlyList<AuditLogEntry>> GetByActorAsync(
        Guid actorId,
        CancellationToken ct = default);
}
