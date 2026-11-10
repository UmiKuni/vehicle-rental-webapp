using CarRental.Modules.Audit.Contracts.Commands;

namespace CarRental.Modules.Audit.EventHandlers;

/// <summary>
/// Wolverine consumer for <see cref="RecordAuditEntry"/>.
/// </summary>
public sealed class RecordAuditEntryConsumer
{
    /*
     * WHAT TO DO IN THIS FILE:
     * ------------------------
     * Triggered by: RecordAuditEntry command dispatched asynchronously through Wolverine Outbox
     *               whenever an audited administrative action or sensitive identity document access occurs.
     * 
     * Responsibilities:
     * 1. Inspect command details:
     *    - command.EventId, command.Timestamp, command.ActorId, command.IpAddress,
     *    - command.Action, command.EntityName, command.EntityId,
     *    - command.BeforeStateJson, command.AfterStateJson.
     * 2. Call AuditLogEntry.Record(...) factory method:
     *    - Validates invariants (non-empty actor ID, action, entity name/ID).
     *    - Automatically masks sensitive PII (CCCD/GPLX numbers) from before/after state payloads.
     * 3. Append the immutable AuditLogEntry to AuditDbContext.AuditLogEntries.
     * 4. Save changes to AuditDbContext.
     */
    public Task Consume(RecordAuditEntry command, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}

