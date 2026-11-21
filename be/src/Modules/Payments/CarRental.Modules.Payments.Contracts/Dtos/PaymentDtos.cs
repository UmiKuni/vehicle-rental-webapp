using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Contracts.Dtos;

public sealed record InitiatePaymentRequest(
    Guid BookingId,
    string Method,
    string ReturnUrl);

public sealed record PaymentDetailDto(
    Guid Id,
    Guid BookingId,
    decimal Amount,
    string Status,
    string Method,
    string GatewayTransactionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt,
    decimal RefundedAmount = 0m,
    DateTimeOffset? RefundedAt = null);

public sealed record RefundPaymentRequest(
    decimal Amount,
    string Reason);

public sealed record PaymentRefundDto(
    Guid PaymentId,
    Guid BookingId,
    string Status,
    decimal OriginalAmount,
    decimal RefundedAmount,
    decimal RemainingAmount,
    string RefundTransactionId,
    string Reason,
    DateTimeOffset RefundedAt);

public sealed record DepositDetailDto(
    Guid Id,
    Guid BookingId,
    decimal Amount,
    string Status,
    decimal DeductedAmount,
    decimal RefundedAmount,
    string? Notes,
    DateTimeOffset UpdatedAt);

public sealed record BookingPaymentSummaryDto(
    Guid BookingId,
    PaymentDetailDto? MainPayment,
    DepositDetailDto? Deposit);

public sealed record PaymentWebhookRequest(
    string TransactionId,
    string OrderId,
    decimal Amount,
    string ResponseCode,
    string Signature);

public sealed record PaymentWebhookResponseDto(
    bool Success,
    string Message);

public sealed record DeductDepositRequest(
    decimal DeductAmount,
    string Reason);

public sealed record ReleaseDepositRequest(string? Note);

public sealed record DepositActionDto(
    Guid DepositId,
    string Status,
    decimal OriginalAmount,
    decimal DeductedAmount,
    decimal RefundedAmount,
    DateTimeOffset Timestamp,
    string? Notes = null);

public sealed record CapturePayPalOrderRequest(string OrderId);

public sealed record VnPayIpnResponseDto(string RspCode, string Message);

public sealed record GatewayWebhookRequest(
    PaymentMethod Method,
    string RawBody,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> QueryParameters);
