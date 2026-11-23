using CarRental.BuildingBlocks.Results;

namespace CarRental.Modules.Audit.Contracts;

/// <summary>
/// Helper extensions for executing operations with transactional audit logging.
/// Ensures audit records are only dispatched when the underlying business operation succeeds.
/// </summary>
public static class AuditRecorderExtensions
{
    public static async Task<Result<T>> ExecuteWithAuditAsync<T>(
        this IAuditRecorder recorder,
        string action,
        string entityName,
        Func<T, string> getEntityId,
        Func<Task<Result<T>>> execute,
        Func<Task<object?>>? captureBeforeState = null,
        Func<T, object?>? captureAfterState = null,
        Guid? actorId = null,
        string? ipAddress = null,
        Guid? eventId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(execute);
        ArgumentNullException.ThrowIfNull(getEntityId);

        object? beforeState = null;
        if (captureBeforeState is not null)
        {
            try
            {
                beforeState = await captureBeforeState();
            }
            catch
            {
                // Before-state capture failure must not abort operation execution
            }
        }

        var result = await execute();
        if (result.IsFailure)
        {
            return result;
        }

        object? afterState = null;
        if (captureAfterState is not null)
        {
            try
            {
                afterState = captureAfterState(result.Value);
            }
            catch
            {
                // After-state capture failure must not abort successful result
            }
        }

        var entityId = getEntityId(result.Value);
        await recorder.RecordAsync(
            action,
            entityName,
            entityId,
            beforeState,
            afterState,
            actorId,
            ipAddress,
            eventId,
            ct);

        return result;
    }

    public static async Task<Result> ExecuteWithAuditAsync(
        this IAuditRecorder recorder,
        string action,
        string entityName,
        string entityId,
        Func<Task<Result>> execute,
        Func<Task<object?>>? captureBeforeState = null,
        Func<object?>? captureAfterState = null,
        Guid? actorId = null,
        string? ipAddress = null,
        Guid? eventId = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(recorder);
        ArgumentNullException.ThrowIfNull(execute);

        object? beforeState = null;
        if (captureBeforeState is not null)
        {
            try
            {
                beforeState = await captureBeforeState();
            }
            catch
            {
                // Before-state capture failure must not abort operation execution
            }
        }

        var result = await execute();
        if (result.IsFailure)
        {
            return result;
        }

        object? afterState = null;
        if (captureAfterState is not null)
        {
            try
            {
                afterState = captureAfterState();
            }
            catch
            {
                // After-state capture failure must not abort successful result
            }
        }

        await recorder.RecordAsync(
            action,
            entityName,
            entityId,
            beforeState,
            afterState,
            actorId,
            ipAddress,
            eventId,
            ct);

        return result;
    }
}
