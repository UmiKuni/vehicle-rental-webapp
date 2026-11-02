using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Contracts.Dtos;

public sealed record PaymentCheckoutDto(
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    PaymentMethod Method,
    string PaymentUrl,
    string QrCodeUrl,
    DateTimeOffset ExpiresAt);

