using System.Text.Json;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Messaging;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Payments.Contracts;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Contracts.Events;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Data.Repositories;
using CarRental.Modules.Payments.Entities;
using CarRental.Modules.Payments.Errors;
using Microsoft.AspNetCore.Http;

namespace CarRental.Modules.Payments.Services;

internal sealed class PaymentsApiService : IPaymentsApi
{
    private readonly PaymentsDbContext _dbContext;
    private readonly IPaymentsRepository _repository;
    private readonly IBookingApi _bookingApi;
    private readonly IPaymentGateway _gateway;
    private readonly IEventOutbox _outbox;
    private readonly TimeProvider _clock;
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public PaymentsApiService(
        PaymentsDbContext dbContext,
        IPaymentsRepository repository,
        IBookingApi bookingApi,
        IPaymentGateway gateway,
        IEventOutbox outbox,
        TimeProvider clock,
        IHttpContextAccessor? httpContextAccessor = null)
    {
        _dbContext = dbContext;
        _repository = repository;
        _bookingApi = bookingApi;
        _gateway = gateway;
        _outbox = outbox;
        _clock = clock;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<Result<PaymentCheckoutDto>> CreateCheckoutAsync(
        Guid bookingId,
        decimal amount,
        decimal depositPortion,
        PaymentMethod method,
        string returnUrl,
        CancellationToken ct = default)
    {
        var totalMoneyResult = ToMoneyResult(amount, PaymentsErrors.InvalidAmount, allowZero: false);
        if (totalMoneyResult.IsFailure)
            return totalMoneyResult.Error;

        var depositMoneyResult = ToMoneyResult(depositPortion, PaymentsErrors.InvalidDepositPortion, allowZero: true);
        if (depositMoneyResult.IsFailure)
            return depositMoneyResult.Error;

        if (!IsValidReturnUrl(returnUrl))
            return PaymentsErrors.InvalidReturnUrl;

        var now = _clock.GetUtcNow();
        var paymentResult = await GetOrCreatePendingPaymentAsync(
            bookingId,
            totalMoneyResult.Value,
            depositMoneyResult.Value,
            method,
            now,
            ct);
        if (paymentResult.IsFailure)
            return paymentResult.Error;

        var payment = paymentResult.Value;
        var clientIp = ExtractClientIp(_httpContextAccessor?.HttpContext);

        var sessionResult = await _gateway.CreateCheckoutSessionAsync(
            new GatewayCheckoutRequest(
                payment.Id.Value,
                payment.BookingId,
                payment.Amount.Amount,
                payment.Amount.Currency,
                payment.Method,
                returnUrl.Trim(),
                now,
                clientIp),
            ct);
        if (sessionResult.IsFailure)
            return sessionResult.Error;

        var session = sessionResult.Value;
        return new PaymentCheckoutDto(
            payment.Id.Value,
            payment.BookingId,
            payment.Amount.Amount,
            payment.Method,
            session.PaymentUrl,
            session.QrCodeUrl,
            session.ExpiresAt);
    }

    public async Task<Result<PaymentCheckoutDto>> InitiateCheckoutAsync(
        Guid customerId,
        InitiatePaymentRequest request,
        CancellationToken ct = default)
    {
        if (!TryParsePaymentMethod(request.Method, out var method))
            return PaymentsErrors.InvalidMethod;

        var detailsResult = await _bookingApi.GetRentalDetailsAsync(request.BookingId, ct);
        if (detailsResult.IsFailure)
            return detailsResult.Error;

        var details = detailsResult.Value;
        if (details.CustomerId != customerId)
            return PaymentsErrors.NotParticipant;

        if (!string.Equals(details.Status, "Confirmed", StringComparison.OrdinalIgnoreCase))
            return PaymentsErrors.BookingNotPayable;

        var totalPayable = details.TotalRentAmount + details.SecurityDepositAmount;
        return await CreateCheckoutAsync(
            details.BookingId,
            totalPayable,
            details.SecurityDepositAmount,
            method,
            request.ReturnUrl,
            ct);
    }

    public async Task<Result<PaymentDetailDto>> GetPaymentByIdAsync(
        Guid paymentId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        var paymentResult = await GetPaymentOrFailAsync(paymentId, ct);
        if (paymentResult.IsFailure)
            return paymentResult.Error;

        var authResult = await EnsureParticipantOrAdminAsync(paymentResult.Value.BookingId, actorId, isAdmin, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        return MapPaymentDetail(paymentResult.Value);
    }

    public async Task<Result<BookingPaymentSummaryDto>> GetPaymentByBookingAsync(
        Guid bookingId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        if (bookingId == Guid.Empty)
            return PaymentsErrors.InvalidBooking;

        var authResult = await EnsureParticipantOrAdminAsync(bookingId, actorId, isAdmin, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var payment = await _repository.GetLatestPaymentByBookingIdAsync(bookingId, ct);
        var deposit = await _repository.GetDepositByBookingIdAsync(bookingId, ct);

        return new BookingPaymentSummaryDto(
            bookingId,
            payment is null ? null : MapPaymentDetail(payment),
            deposit is null ? null : MapDepositDetail(deposit, _clock.GetUtcNow()));
    }

    public async Task<Result<PaymentWebhookResponseDto>> HandleWebhookAsync(
        PaymentWebhookRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            return PaymentsErrors.WebhookInvalidOrder;

        if (!_gateway.VerifyWebhookSignature(request))
            return PaymentsErrors.InvalidSignature;

        if (string.IsNullOrWhiteSpace(request.TransactionId))
            return PaymentsErrors.InvalidTransactionId;

        var transactionId = request.TransactionId.Trim();
        if (await _repository.HasWebhookBeenProcessedAsync(transactionId, ct))
            return new PaymentWebhookResponseDto(true, "Webhook already processed.");

        if (!Guid.TryParse(request.OrderId, out var orderGuid) || orderGuid == Guid.Empty)
            return PaymentsErrors.WebhookInvalidOrder;

        var payment = await _repository.GetPaymentByIdAsync(PaymentId.From(orderGuid), ct)
                      ?? await _repository.GetPendingPaymentByBookingIdAsync(orderGuid, ct);

        if (payment is null)
            return PaymentsErrors.PaymentNotFound;

        if (request.Amount != payment.Amount.Amount)
            return PaymentsErrors.AmountMismatch;

        var now = _clock.GetUtcNow();
        var payloadJson = JsonSerializer.Serialize(request);

        return string.Equals(request.ResponseCode?.Trim(), "00", StringComparison.Ordinal)
            ? await HandleWebhookSuccessAsync(payment, transactionId, payloadJson, now, ct)
            : await HandleWebhookFailureAsync(payment, transactionId, request.ResponseCode, payloadJson, now, ct);
    }

    public async Task<Result<PaymentWebhookResponseDto>> HandleGatewayWebhookAsync(
        GatewayWebhookRequest request,
        CancellationToken ct = default)
    {
        if (request is null)
            return PaymentsErrors.WebhookPayloadInvalid("Webhook request is required.");

        var parseResult = await _gateway.ParseAndVerifyWebhookAsync(
            new GatewayRawWebhook(
                request.Method,
                request.RawBody,
                request.Headers,
                request.QueryParameters),
            ct);

        if (parseResult.IsFailure)
            return parseResult.Error;

        var notification = parseResult.Value;
        if (notification.IsIgnored)
            return new PaymentWebhookResponseDto(true, notification.IgnoredReason ?? "Webhook event ignored.");

        if (!string.IsNullOrWhiteSpace(notification.OrderIdToCapture))
        {
            var captureResult = await _gateway.CapturePayPalOrderAsync(notification.OrderIdToCapture, ct);
            if (captureResult.IsFailure)
                return captureResult.Error;

            notification = captureResult.Value;
        }

        return await ProcessGatewayNotificationAsync(notification, ct);
    }

    public async Task<Result<PaymentDetailDto>> CapturePayPalOrderAsync(
        string paypalOrderId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(paypalOrderId))
            return PaymentsErrors.InvalidPayPalOrderId;

        var captureResult = await _gateway.CapturePayPalOrderAsync(paypalOrderId.Trim(), ct);
        if (captureResult.IsFailure)
            return captureResult.Error;

        var notification = captureResult.Value;
        if (string.IsNullOrWhiteSpace(notification.TransactionId))
            return PaymentsErrors.InvalidTransactionId;

        if (!Guid.TryParse(notification.OrderId, out var orderGuid) || orderGuid == Guid.Empty)
            return PaymentsErrors.WebhookInvalidOrder;

        var payment = await _repository.GetPaymentByIdAsync(PaymentId.From(orderGuid), ct)
                      ?? await _repository.GetPendingPaymentByBookingIdAsync(orderGuid, ct);

        if (payment is null)
            return PaymentsErrors.PaymentNotFound;

        var authResult = await EnsureParticipantOrAdminAsync(payment.BookingId, actorId, isAdmin, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var transactionId = notification.TransactionId.Trim();
        if (await _repository.HasWebhookBeenProcessedAsync(transactionId, ct)
            || payment.Status == PaymentStatus.Success)
        {
            return MapPaymentDetail(payment);
        }

        if (!_gateway.IsWebhookAmountValid(
                payment.Method,
                payment.Amount.Amount,
                payment.Amount.Currency,
                notification.Amount,
                notification.Currency))
        {
            return PaymentsErrors.AmountMismatch;
        }

        var now = _clock.GetUtcNow();
        var processResult = notification.IsSuccess
            ? await HandleWebhookSuccessAsync(payment, transactionId, notification.RawPayloadJson, now, ct)
            : await HandleWebhookFailureAsync(payment, transactionId, notification.ResponseCode, notification.RawPayloadJson, now, ct);

        if (processResult.IsFailure)
            return processResult.Error;

        return MapPaymentDetail(payment);
    }

    public async Task<Result<PaymentRefundDto>> RefundPaymentAsync(
        Guid paymentId,
        Guid actorId,
        bool isAdmin,
        decimal amount,
        string reason,
        CancellationToken ct = default)
    {
        var paymentResult = await GetPaymentOrFailAsync(paymentId, ct);
        if (paymentResult.IsFailure)
            return paymentResult.Error;

        var payment = paymentResult.Value;
        var authResult = await EnsureDepositOwnerOrAdminAsync(payment.BookingId, actorId, isAdmin, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var refundMoneyResult = ToMoneyResult(amount, PaymentsErrors.InvalidRefundAmount, allowZero: false, payment.Amount.Currency);
        if (refundMoneyResult.IsFailure)
            return refundMoneyResult.Error;

        var trimmedReason = reason?.Trim() ?? string.Empty;
        var validation = payment.ValidateRefund(refundMoneyResult.Value, trimmedReason);
        if (validation.IsFailure)
            return validation.Error;

        var gatewayResult = await _gateway.RefundAsync(
            new GatewayRefundRequest(
                payment.Id.Value,
                payment.Method,
                payment.GatewayTransactionId!,
                refundMoneyResult.Value.Amount,
                payment.Amount.Amount,
                payment.Amount.Currency,
                trimmedReason,
                payment.CompletedAt),
            ct);

        if (gatewayResult.IsFailure)
            return gatewayResult.Error;

        var refundResult = payment.Refund(
            refundMoneyResult.Value,
            gatewayResult.Value.RefundTransactionId,
            trimmedReason,
            gatewayResult.Value.ProcessedAt);

        if (refundResult.IsFailure)
            return refundResult.Error;

        await _dbContext.SaveChangesAsync(ct);

        await _outbox.PublishAsync(
            new PaymentRefunded(
                Guid.CreateVersion7(),
                gatewayResult.Value.ProcessedAt,
                payment.BookingId,
                payment.Id.Value,
                refundMoneyResult.Value,
                payment.Status == PaymentStatus.Refunded,
                trimmedReason),
            ct);

        var totalRefunded = payment.RefundedAmount.Amount;
        return new PaymentRefundDto(
            payment.Id.Value,
            payment.BookingId,
            payment.Status.ToString(),
            payment.Amount.Amount,
            totalRefunded,
            payment.Amount.Amount - totalRefunded,
            payment.RefundTransactionId!,
            payment.RefundReason!,
            payment.RefundedAt!.Value);
    }

    public async Task<Result<DepositActionDto>> ReleaseDepositByIdAsync(
        Guid depositId,
        Guid actorId,
        bool isAdmin = false,
        string? note = null,
        CancellationToken ct = default)
    {
        var depositResult = await GetDepositByIdOrFailAsync(depositId, ct);
        if (depositResult.IsFailure)
            return depositResult.Error;

        var deposit = depositResult.Value;
        var authResult = await EnsureDepositOwnerOrAdminAsync(deposit.BookingId, actorId, isAdmin, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var now = _clock.GetUtcNow();
        var releaseResult = deposit.Release(now, note);
        if (releaseResult.IsFailure)
            return releaseResult.Error;

        await _dbContext.SaveChangesAsync(ct);
        return MapDepositAction(deposit, now);
    }

    public async Task<Result<DepositActionDto>> DeductDepositByIdAsync(
        Guid depositId,
        Guid actorId,
        bool isAdmin = false,
        decimal deductAmount = 0m,
        string reason = "",
        CancellationToken ct = default)
    {
        var depositResult = await GetDepositByIdOrFailAsync(depositId, ct);
        if (depositResult.IsFailure)
            return depositResult.Error;

        var deposit = depositResult.Value;
        var authResult = await EnsureDepositOwnerOrAdminAsync(deposit.BookingId, actorId, isAdmin, ct);
        if (authResult.IsFailure)
            return authResult.Error;

        var deductMoneyResult = ToMoneyResult(deductAmount, PaymentsErrors.InvalidDeductAmount, allowZero: false, deposit.Amount.Currency);
        if (deductMoneyResult.IsFailure)
            return deductMoneyResult.Error;

        var now = _clock.GetUtcNow();
        var deductResult = deposit.Deduct(deductMoneyResult.Value, now, reason);
        if (deductResult.IsFailure)
            return deductResult.Error;

        await _dbContext.SaveChangesAsync(ct);
        return MapDepositAction(deposit, now);
    }

    public async Task<Result> FreezeDepositAsync(Guid bookingId, CancellationToken ct = default)
    {
        var depositResult = await GetDepositByBookingOrFailAsync(bookingId, ct);
        if (depositResult.IsFailure)
            return depositResult.Error;

        depositResult.Value.Freeze();
        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> SettleDepositAsync(Guid bookingId, int depositToCustomerPercent, CancellationToken ct = default)
    {
        var depositResult = await GetDepositByBookingOrFailAsync(bookingId, ct);
        if (depositResult.IsFailure)
            return depositResult.Error;

        var settleResult = depositResult.Value.SettleByRuling(depositToCustomerPercent, _clock.GetUtcNow());
        if (settleResult.IsFailure)
            return settleResult;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result> ReleaseDepositAsync(Guid bookingId, CancellationToken ct = default)
    {
        var depositResult = await GetDepositByBookingOrFailAsync(bookingId, ct);
        if (depositResult.IsFailure)
            return depositResult.Error;

        var releaseResult = depositResult.Value.Release(_clock.GetUtcNow());
        if (releaseResult.IsFailure)
            return releaseResult;

        await _dbContext.SaveChangesAsync(ct);
        return Result.Success();
    }

    public async Task<Result<DepositDetailsDto>> GetDepositAsync(Guid bookingId, CancellationToken ct = default)
    {
        var depositResult = await GetDepositByBookingOrFailAsync(bookingId, ct);
        if (depositResult.IsFailure)
            return depositResult.Error;

        var deposit = depositResult.Value;
        return new DepositDetailsDto(
            deposit.Id.Value,
            deposit.BookingId,
            deposit.Amount.Amount,
            deposit.Status,
            deposit.DeductedAmount.Amount,
            deposit.IsFrozen,
            deposit.ReleaseAfter,
            deposit.SettledAt,
            deposit.Notes);
    }

    private async Task<Result<PaymentWebhookResponseDto>> ProcessGatewayNotificationAsync(
        GatewayWebhookNotification notification,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(notification.TransactionId))
            return PaymentsErrors.InvalidTransactionId;

        var transactionId = notification.TransactionId.Trim();
        if (await _repository.HasWebhookBeenProcessedAsync(transactionId, ct))
            return new PaymentWebhookResponseDto(true, "Webhook already processed.");

        if (!Guid.TryParse(notification.OrderId, out var orderGuid) || orderGuid == Guid.Empty)
            return PaymentsErrors.WebhookInvalidOrder;

        var payment = await _repository.GetPaymentByIdAsync(PaymentId.From(orderGuid), ct)
                      ?? await _repository.GetPendingPaymentByBookingIdAsync(orderGuid, ct);

        if (payment is null)
            return PaymentsErrors.PaymentNotFound;

        if (!_gateway.IsWebhookAmountValid(
                payment.Method,
                payment.Amount.Amount,
                payment.Amount.Currency,
                notification.Amount,
                notification.Currency))
        {
            return PaymentsErrors.AmountMismatch;
        }

        var now = _clock.GetUtcNow();
        return notification.IsSuccess
            ? await HandleWebhookSuccessAsync(payment, transactionId, notification.RawPayloadJson, now, ct)
            : await HandleWebhookFailureAsync(payment, transactionId, notification.ResponseCode, notification.RawPayloadJson, now, ct);
    }

    private async Task<Result<Payment>> GetOrCreatePendingPaymentAsync(
        Guid bookingId,
        Money totalMoney,
        Money depositMoney,
        PaymentMethod method,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var existing = await _repository.GetPendingPaymentByBookingIdAsync(bookingId, ct);
        if (existing is not null)
        {
            if (existing.Method == method)
                return existing;

            var updateMethodResult = existing.SetPaymentMethod(method);
            if (updateMethodResult.IsFailure)
                return updateMethodResult.Error;

            await _dbContext.SaveChangesAsync(ct);
            return existing;
        }

        var createResult = Payment.CreatePending(bookingId, totalMoney, depositMoney, now);
        if (createResult.IsFailure)
            return createResult.Error;

        var payment = createResult.Value;
        var setMethodResult = payment.SetPaymentMethod(method);
        if (setMethodResult.IsFailure)
            return setMethodResult.Error;

        _repository.AddPayment(payment);
        await _dbContext.SaveChangesAsync(ct);
        return payment;
    }

    private async Task<Result<PaymentWebhookResponseDto>> HandleWebhookSuccessAsync(
        Payment payment,
        string transactionId,
        string payloadJson,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var markResult = payment.MarkSuccess(transactionId, now);
        if (markResult.IsFailure)
            return markResult.Error;

        var depositHoldResult = await EnsureDepositHeldAsync(payment, now, ct);
        if (depositHoldResult.IsFailure)
            return depositHoldResult.Error;

        _repository.AddWebhookInbox(new WebhookInbox(transactionId, now, payloadJson));
        await _dbContext.SaveChangesAsync(ct);

        var markPaidResult = await _bookingApi.MarkPaidAsync(payment.BookingId, payment.Id.Value, now, ct);
        if (markPaidResult.IsFailure)
            return markPaidResult.Error;

        await _outbox.PublishAsync(
            new PaymentSucceeded(Guid.CreateVersion7(), now, payment.BookingId, payment.Id.Value, payment.Amount),
            ct);

        return new PaymentWebhookResponseDto(true, "Payment processed successfully.");
    }

    private async Task<Result<PaymentWebhookResponseDto>> HandleWebhookFailureAsync(
        Payment payment,
        string transactionId,
        string? responseCode,
        string payloadJson,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var failureReason = $"Gateway error code: {responseCode}";
        var markFailedResult = payment.MarkFailed(transactionId, failureReason, now);
        if (markFailedResult.IsFailure)
            return markFailedResult.Error;

        _repository.AddWebhookInbox(new WebhookInbox(transactionId, now, payloadJson));
        await _dbContext.SaveChangesAsync(ct);

        await _outbox.PublishAsync(
            new PaymentFailed(Guid.CreateVersion7(), now, payment.BookingId, payment.Id.Value, failureReason),
            ct);

        return new PaymentWebhookResponseDto(true, "Payment failure recorded.");
    }

    private async Task<Result> EnsureDepositHeldAsync(Payment payment, DateTimeOffset now, CancellationToken ct)
    {
        if (payment.DepositPortion.Amount <= 0)
            return Result.Success();

        var existingDeposit = await _repository.GetDepositByBookingIdAsync(payment.BookingId, ct);
        if (existingDeposit is not null)
            return Result.Success();

        var holdResult = Deposit.Hold(payment.BookingId, payment.DepositPortion, now);
        if (holdResult.IsFailure)
            return holdResult.Error;

        _repository.AddDeposit(holdResult.Value);
        return Result.Success();
    }

    private async Task<Result<Payment>> GetPaymentOrFailAsync(Guid paymentId, CancellationToken ct)
    {
        if (paymentId == Guid.Empty)
            return PaymentsErrors.PaymentNotFound;

        var payment = await _repository.GetPaymentByIdAsync(PaymentId.From(paymentId), ct);
        return payment is not null ? payment : PaymentsErrors.PaymentNotFound;
    }

    private async Task<Result<Deposit>> GetDepositByIdOrFailAsync(Guid depositId, CancellationToken ct)
    {
        if (depositId == Guid.Empty)
            return PaymentsErrors.DepositNotFound;

        var deposit = await _repository.GetDepositByIdAsync(DepositId.From(depositId), ct);
        return deposit is not null ? deposit : PaymentsErrors.DepositNotFound;
    }

    private async Task<Result<Deposit>> GetDepositByBookingOrFailAsync(Guid bookingId, CancellationToken ct)
    {
        if (bookingId == Guid.Empty)
            return PaymentsErrors.InvalidBooking;

        var deposit = await _repository.GetDepositByBookingIdAsync(bookingId, ct);
        return deposit is not null ? deposit : PaymentsErrors.DepositNotFound;
    }

    private async Task<Result> EnsureParticipantOrAdminAsync(
        Guid bookingId,
        Guid actorId,
        bool isAdmin,
        CancellationToken ct)
    {
        if (isAdmin || await _bookingApi.IsParticipantAsync(bookingId, actorId, ct))
            return Result.Success();

        return PaymentsErrors.NotParticipant;
    }

    private async Task<Result> EnsureDepositOwnerOrAdminAsync(
        Guid bookingId,
        Guid actorId,
        bool isAdmin,
        CancellationToken ct)
    {
        if (isAdmin)
            return Result.Success();

        var detailsResult = await _bookingApi.GetRentalDetailsAsync(bookingId, ct);
        if (detailsResult.IsFailure)
            return detailsResult.Error;

        return detailsResult.Value.OwnerId == actorId
            ? Result.Success()
            : PaymentsErrors.NotOwner;
    }

    private static string? ExtractClientIp(HttpContext? httpContext)
    {
        if (httpContext is null)
            return null;

        if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor)
            && !string.IsNullOrWhiteSpace(forwardedFor))
        {
            return forwardedFor.ToString();
        }

        return httpContext.Connection.RemoteIpAddress?.ToString();
    }

    private static Result<Money> ToMoneyResult(
        decimal amount,
        Error invalidError,
        bool allowZero,
        string currency = Money.DefaultCurrency)
    {
        var rounded = (long)Math.Round(amount, MidpointRounding.AwayFromZero);
        if (rounded < 0 || (!allowZero && rounded == 0))
            return invalidError;

        return Money.Of(rounded, currency);
    }

    private static bool TryParsePaymentMethod(string? value, out PaymentMethod method)
    {
        method = default;
        if (string.IsNullOrWhiteSpace(value) || int.TryParse(value, out _))
            return false;

        return Enum.TryParse(value.Trim(), ignoreCase: true, out method) && Enum.IsDefined(method);
    }

    private static bool IsValidReturnUrl(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl)
        && Uri.TryCreate(returnUrl.Trim(), UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static PaymentDetailDto MapPaymentDetail(Payment payment) =>
        new(
            payment.Id.Value,
            payment.BookingId,
            payment.Amount.Amount,
            payment.Status.ToString(),
            payment.Method.ToString(),
            payment.GatewayTransactionId ?? string.Empty,
            payment.CreatedAt,
            payment.CompletedAt,
            payment.RefundedAmount?.Amount ?? 0m,
            payment.RefundedAt);

    private static DepositDetailDto MapDepositDetail(Deposit deposit, DateTimeOffset now) =>
        new(
            deposit.Id.Value,
            deposit.BookingId,
            deposit.Amount.Amount,
            deposit.Status.ToString(),
            deposit.DeductedAmount.Amount,
            CalculateRefundedAmount(deposit),
            deposit.IsFrozen ? "Deposit is frozen pending dispute resolution." : deposit.Notes,
            deposit.SettledAt ?? now);

    private static DepositActionDto MapDepositAction(Deposit deposit, DateTimeOffset timestamp) =>
        new(
            deposit.Id.Value,
            deposit.Status.ToString(),
            deposit.Amount.Amount,
            deposit.DeductedAmount.Amount,
            CalculateRefundedAmount(deposit),
            deposit.SettledAt ?? timestamp,
            deposit.Notes);

    private static decimal CalculateRefundedAmount(Deposit deposit) => deposit.Status switch
    {
        DepositStatus.Released => deposit.Amount.Amount,
        DepositStatus.Deducted => Math.Max(0m, deposit.Amount.Amount - deposit.DeductedAmount.Amount),
        _ => 0m
    };
}
