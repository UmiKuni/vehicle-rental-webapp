using System.Text.RegularExpressions;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Audit.Entities;

/// <summary>
/// Append-only audit log entry capturing administrative and sensitive domain actions
/// for security, compliance, and dispute resolution.
/// </summary>
public sealed class AuditLogEntry : Entity<Guid>
{
    private static readonly Regex CccdOrGplxRegex = new(@"\b\d{8,12}\b", RegexOptions.Compiled);

    public DateTimeOffset Timestamp { get; private set; }
    public Guid ActorId { get; private set; }
    public string IpAddress { get; private set; } = string.Empty;
    public string Action { get; private set; } = string.Empty;
    public string EntityName { get; private set; } = string.Empty;
    public string EntityId { get; private set; } = string.Empty;
    public string? BeforeState { get; private set; }
    public string? AfterState { get; private set; }

    private AuditLogEntry()
    {
    }

    private AuditLogEntry(
        Guid id,
        DateTimeOffset timestamp,
        Guid actorId,
        string ipAddress,
        string action,
        string entityName,
        string entityId,
        string? beforeState,
        string? afterState)
    {
        Id = id;
        Timestamp = timestamp;
        ActorId = actorId;
        IpAddress = ipAddress;
        Action = action;
        EntityName = entityName;
        EntityId = entityId;
        BeforeState = beforeState;
        AfterState = afterState;
    }

    /// <summary>
    /// Factory method to record an immutable audit entry.
    /// Invariants: ActorId, Action, EntityName, and EntityId are required.
    /// PII in state snapshots is sanitized.
    /// </summary>
    public static Result<AuditLogEntry> Record(
        Guid id,
        DateTimeOffset timestamp,
        Guid actorId,
        string ipAddress,
        string action,
        string entityName,
        string entityId,
        string? beforeState = null,
        string? afterState = null)
    {
        if (actorId == Guid.Empty)
        {
            return Result.Failure<AuditLogEntry>(Error.Validation(
                "Audit.EmptyActorId",
                "Actor ID must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            return Result.Failure<AuditLogEntry>(Error.Validation(
                "Audit.EmptyAction",
                "Action must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(entityName))
        {
            return Result.Failure<AuditLogEntry>(Error.Validation(
                "Audit.EmptyEntityName",
                "Entity name must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(entityId))
        {
            return Result.Failure<AuditLogEntry>(Error.Validation(
                "Audit.EmptyEntityId",
                "Entity ID must not be empty."));
        }

        var entryId = id == Guid.Empty ? Guid.NewGuid() : id;
        var entryTimestamp = timestamp == default ? DateTimeOffset.UtcNow : timestamp;
        var entryIp = string.IsNullOrWhiteSpace(ipAddress) ? "0.0.0.0" : ipAddress.Trim();

        var sanitizedBefore = MaskPii(beforeState);
        var sanitizedAfter = MaskPii(afterState);

        return Result.Success(new AuditLogEntry(
            entryId,
            entryTimestamp,
            actorId,
            entryIp,
            action.Trim(),
            entityName.Trim(),
            entityId.Trim(),
            sanitizedBefore,
            sanitizedAfter));
    }

    private static string? MaskPii(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return json;
        }

        // Mask 8-12 consecutive digits (e.g. CCCD, CMND, GPLX) preserving only last 3 digits
        return CccdOrGplxRegex.Replace(json, match =>
        {
            var val = match.Value;
            return new string('*', val.Length - 3) + val[^3..];
        });
    }
}
