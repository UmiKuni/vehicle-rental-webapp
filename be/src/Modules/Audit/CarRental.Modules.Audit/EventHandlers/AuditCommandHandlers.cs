using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Audit.Contracts.Commands;
using CarRental.Modules.Audit.Data;
using CarRental.Modules.Audit.Data.Repositories;
using CarRental.Modules.Audit.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Audit.EventHandlers;

public static class AuditCommandHandlers
{
    public static async Task Handle(
        RecordAuditEntry command,
        AuditDbContext dbContext,
        IAuditLogRepository repository,
        CancellationToken ct)
    {
        if (command.EventId == Guid.Empty)
            throw new DomainException("Audit.EmptyEventId", "Audit event ID must not be empty.");

        // The event ID is the entry ID, making retries safe and preventing duplicate records.
        if (await dbContext.AuditLogEntries.AnyAsync(entry => entry.Id == command.EventId, ct))
            return;

        var result = AuditLogEntry.Record(
            command.EventId,
            command.Timestamp,
            command.ActorId,
            command.IpAddress,
            command.Action,
            command.EntityName,
            command.EntityId,
            command.BeforeStateJson,
            command.AfterStateJson);

        if (result.IsFailure)
            throw new DomainException(result.Error.Code, result.Error.Message);

        await repository.AddAsync(result.Value, ct);
        await dbContext.SaveChangesAsync(ct);
    }
}
