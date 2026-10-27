using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Payments.Endpoints;

public static class PaymentEndpoints
{
    public static IEndpointRouteBuilder MapPaymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/payments")
            .WithTags("Payments");

        // 1. Create payment checkout session
        group.MapPost("/checkout", (InitiatePaymentRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Method))
            {
                return Results.BadRequest(new { error = "Payment method is required." });
            }

            var paymentId = Guid.NewGuid();
            var checkout = new PaymentCheckoutDto(
                PaymentId: paymentId,
                BookingId: request.BookingId,
                Amount: 3_200_000m,
                Method: request.Method,
                PaymentUrl: $"https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_TxnRef={paymentId:N}&vnp_Amount=320000000",
                QrCodeUrl: "https://api.qrserver.com/v1/create-qr-code/?size=250x250&data=00020101021238580010A000000727",
                ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(15));

            return Results.Ok(checkout);
        })
        .WithName("CreatePaymentCheckout")
        .WithSummary("Khởi tạo phiên thanh toán (VNPay / MoMo)")
        .WithDescription("Initiates an online checkout session with payment gateway (VNPay, MoMo, Bank Transfer) for a booking.")
        .Produces<PaymentCheckoutDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 2. Get payment transaction details by ID
        group.MapGet("/{id:guid}", (Guid id) =>
        {
            var payment = new PaymentDetailDto(
                Id: id,
                BookingId: Guid.Parse("a1111111-0000-0000-0000-000000000001"),
                Amount: 3_200_000m,
                Status: "Success",
                Method: "VNPay",
                GatewayTransactionId: "VNP14829103",
                CreatedAt: DateTimeOffset.UtcNow.AddHours(-1),
                ProcessedAt: DateTimeOffset.UtcNow.AddHours(-1).AddMinutes(2));

            return Results.Ok(payment);
        })
        .WithName("GetPaymentById")
        .WithSummary("Xem chi tiết giao dịch thanh toán")
        .WithDescription("Retrieves details and execution status of a specific payment transaction.")
        .Produces<PaymentDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 3. Get payment & deposit summary by Booking ID
        group.MapGet("/booking/{bookingId:guid}", (Guid bookingId) =>
        {
            var mainPayment = new PaymentDetailDto(
                Id: Guid.NewGuid(),
                BookingId: bookingId,
                Amount: 3_200_000m,
                Status: "Success",
                Method: "VNPay",
                GatewayTransactionId: "VNP14829103",
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-1),
                ProcessedAt: DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(2));

            var deposit = new DepositDetailDto(
                Id: Guid.NewGuid(),
                BookingId: bookingId,
                Amount: 5_000_000m,
                Status: "Held",
                DeductedAmount: 0m,
                RefundedAmount: 0m,
                Notes: "Khóa thẻ tiền cọc 5.000.000 VND thành công",
                UpdatedAt: DateTimeOffset.UtcNow.AddDays(-1).AddMinutes(2));

            var summary = new BookingPaymentSummaryDto(
                BookingId: bookingId,
                MainPayment: mainPayment,
                Deposit: deposit);

            return Results.Ok(summary);
        })
        .WithName("GetPaymentByBooking")
        .WithSummary("Xem tình trạng thanh toán & cọc của đơn")
        .WithDescription("Retrieves all payments and security deposit status associated with a booking.")
        .Produces<BookingPaymentSummaryDto>(StatusCodes.Status200OK);

        // 4. IPN Webhook callback from payment gateway
        group.MapPost("/webhook", (PaymentWebhookRequest request) =>
        {
            var response = new PaymentWebhookResponseDto(
                Success: true,
                Message: "Xử lý IPN thanh toán thành công.");

            return Results.Ok(response);
        })
        .WithName("HandlePaymentWebhook")
        .WithSummary("IPN Webhook callback từ cổng thanh toán")
        .WithDescription("Receives asynchronous Instant Payment Notifications (IPN) from payment gateways (VNPay / MoMo) to update transaction and booking status.")
        .Produces<PaymentWebhookResponseDto>(StatusCodes.Status200OK);

        // 5. Release full deposit back to customer
        group.MapPost("/deposits/{depositId:guid}/release", (Guid depositId, ReleaseDepositRequest? request) =>
        {
            var result = new DepositActionDto(
                DepositId: depositId,
                Status: "Released",
                OriginalAmount: 5_000_000m,
                DeductedAmount: 0m,
                RefundedAmount: 5_000_000m,
                Timestamp: DateTimeOffset.UtcNow);

            return Results.Ok(result);
        })
        .WithName("ReleaseDeposit")
        .WithSummary("Hoàn trả toàn bộ tiền cọc sau khi trả xe")
        .WithDescription("Releases the full security deposit back to the customer after vehicle return inspection passes.")
        .Produces<DepositActionDto>(StatusCodes.Status200OK);

        // 6. Deduct amount from security deposit
        group.MapPost("/deposits/{depositId:guid}/deduct", (Guid depositId, DeductDepositRequest request) =>
        {
            if (request.DeductAmount <= 0)
            {
                return Results.BadRequest(new { error = "DeductAmount must be greater than zero." });
            }

            decimal originalDeposit = 5_000_000m;
            decimal deducted = Math.Min(originalDeposit, request.DeductAmount);
            decimal refunded = Math.Max(0m, originalDeposit - deducted);

            var result = new DepositActionDto(
                DepositId: depositId,
                Status: "Deducted",
                OriginalAmount: originalDeposit,
                DeductedAmount: deducted,
                RefundedAmount: refunded,
                Timestamp: DateTimeOffset.UtcNow);

            return Results.Ok(result);
        })
        .WithName("DeductDeposit")
        .WithSummary("Khấu trừ cọc do hư hỏng, quá km, trễ hạn")
        .WithDescription("Deducts a specified amount from the security deposit for repair costs, over-mileage, or late returns, refunding the remainder.")
        .Produces<DepositActionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}

public sealed record InitiatePaymentRequest(
    Guid BookingId,
    string Method, // VNPay, Momo, Transfer
    string ReturnUrl);

public sealed record PaymentCheckoutDto(
    Guid PaymentId,
    Guid BookingId,
    decimal Amount,
    string Method,
    string PaymentUrl,
    string QrCodeUrl,
    DateTimeOffset ExpiresAt);

public sealed record PaymentDetailDto(
    Guid Id,
    Guid BookingId,
    decimal Amount,
    string Status, // Pending, Success, Failed
    string Method,
    string GatewayTransactionId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ProcessedAt);

public sealed record DepositDetailDto(
    Guid Id,
    Guid BookingId,
    decimal Amount,
    string Status, // Held, Released, Deducted, Forfeited
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
    string ResponseCode, // "00" = Success
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
    DateTimeOffset Timestamp);
