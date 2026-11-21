using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;

namespace CarRental.Modules.Payments.Contracts.Events;

public sealed record PaymentRefunded(
    Guid EventId,
    DateTimeOffset OccurredAt,
    Guid BookingId,
    Guid PaymentId,
    Money RefundedAmount,
    bool IsFullRefund,
    string Reason) : IntegrationEvent(EventId, OccurredAt);
