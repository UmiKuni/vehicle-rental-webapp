using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CarRental.Modules.Audit.Contracts;
using CarRental.Modules.Audit.Contracts.Commands;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Wolverine;

namespace CarRental.Modules.Audit.Services;

/// <summary>
/// Scoped audit recorder that captures execution context (actor, IP, timestamp)
/// and publishes immutable audit entries through the Wolverine transactional outbox.
/// </summary>
public sealed class AuditRecorder : IAuditRecorder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly IMessageBus _bus;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AuditRecorder> _logger;

    public AuditRecorder(
        IMessageBus bus,
        TimeProvider timeProvider,
        ILogger<AuditRecorder> logger,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _bus = bus ?? throw new ArgumentNullException(nameof(bus));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpContextAccessor = httpContextAccessor;
    }

    public Task RecordAsync(
        IAuditedAction action,
        object? beforeState = null,
        object? afterState = null,
        Guid? actorId = null,
        string? ipAddress = null,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(action);

        return RecordAsync(
            action.Action,
            action.EntityName,
            action.EntityId,
            action.IncludeBeforeState ? beforeState : null,
            action.IncludeAfterState ? afterState : null,
            actorId,
            ipAddress,
            action.EventId,
            ct);
    }

    public async Task RecordAsync(
        string action,
        string entityName,
        string entityId,
        object? beforeState = null,
        object? afterState = null,
        Guid? actorId = null,
        string? ipAddress = null,
        Guid? eventId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action must not be empty.", nameof(action));
        if (string.IsNullOrWhiteSpace(entityName))
            throw new ArgumentException("Entity name must not be empty.", nameof(entityName));
        if (string.IsNullOrWhiteSpace(entityId))
            throw new ArgumentException("Entity ID must not be empty.", nameof(entityId));

        var resolvedActorId = ResolveActorId(actorId);
        var resolvedIpAddress = ResolveIpAddress(ipAddress);
        var timestamp = _timeProvider.GetUtcNow();
        var entryEventId = eventId ?? Guid.NewGuid();

        var beforeStateJson = SerializeState(beforeState);
        var afterStateJson = SerializeState(afterState);

        var command = new RecordAuditEntry(
            entryEventId,
            timestamp,
            resolvedActorId,
            resolvedIpAddress,
            action.Trim(),
            entityName.Trim(),
            entityId.Trim(),
            beforeStateJson,
            afterStateJson);

        try
        {
            await _bus.PublishAsync(command);
            _logger.LogInformation(
                "Dispatched audit entry {EventId} for action {Action} on {EntityName}:{EntityId} by actor {ActorId}",
                entryEventId, action, entityName, entityId, resolvedActorId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to dispatch audit entry {EventId} for action {Action} on {EntityName}:{EntityId} by actor {ActorId}",
                entryEventId, action, entityName, entityId, resolvedActorId);
            throw;
        }
    }

    private Guid ResolveActorId(Guid? actorId)
    {
        if (actorId.HasValue && actorId.Value != Guid.Empty)
        {
            return actorId.Value;
        }

        var httpContext = _httpContextAccessor?.HttpContext;
        var claim = httpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (Guid.TryParse(claim, out var parsedId) && parsedId != Guid.Empty)
        {
            return parsedId;
        }

        throw new UnauthorizedAccessException("Audit recording requires an authenticated actor identifier.");
    }

    private string ResolveIpAddress(string? ipAddress)
    {
        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            return ipAddress.Trim();
        }

        var httpContext = _httpContextAccessor?.HttpContext;
        if (httpContext is not null)
        {
            if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor)
                && !string.IsNullOrWhiteSpace(forwardedFor))
            {
                var ips = forwardedFor.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (ips.Length > 0 && !string.IsNullOrWhiteSpace(ips[0]))
                {
                    return ips[0];
                }
            }

            var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrWhiteSpace(remoteIp))
            {
                return remoteIp;
            }
        }

        return "0.0.0.0";
    }

    private static string? SerializeState(object? state)
    {
        if (state is null)
            return null;

        if (state is string str)
            return string.IsNullOrWhiteSpace(str) ? null : str.Trim();

        return JsonSerializer.Serialize(state, JsonOptions);
    }
}
