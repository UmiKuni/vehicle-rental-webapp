using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Payments.Contracts;
using CarRental.Modules.Payments.Contracts.Dtos;
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

        group.MapPost("/checkout", async (
            InitiatePaymentRequest request,
            ClaimsPrincipal principal,
            IPaymentsApi api,
            CancellationToken ct) =>
            (await api.InitiateCheckoutAsync(GetUserId(principal), request, ct)).ToHttpResult())
        .WithValidation<InitiatePaymentRequest>()
        .WithName("CreatePaymentCheckout")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Khởi tạo phiên thanh toán (VNPay / PayPal / Stripe / Transfer)")
        .WithDescription("Initiates an online checkout session with payment gateway (VNPay, PayPal, Stripe, Bank Transfer) for a booking.")
        .Produces<PaymentCheckoutDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IPaymentsApi api,
            CancellationToken ct) =>
            (await api.GetPaymentByIdAsync(id, GetUserId(principal), principal.IsInRole("Admin"), ct)).ToHttpResult())
        .WithName("GetPaymentById")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Xem chi tiết giao dịch thanh toán")
        .WithDescription("Retrieves details and execution status of a specific payment transaction.")
        .Produces<PaymentDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapGet("/booking/{bookingId:guid}", async (
            Guid bookingId,
            ClaimsPrincipal principal,
            IPaymentsApi api,
            CancellationToken ct) =>
            (await api.GetPaymentByBookingAsync(bookingId, GetUserId(principal), principal.IsInRole("Admin"), ct)).ToHttpResult())
        .WithName("GetPaymentByBooking")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Xem tình trạng thanh toán & cọc của đơn")
        .WithDescription("Retrieves all payments and security deposit status associated with a booking.")
        .Produces<BookingPaymentSummaryDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapPost("/webhook", async (
            PaymentWebhookRequest request,
            IPaymentsApi api,
            CancellationToken ct) =>
            (await api.HandleWebhookAsync(request, ct)).ToHttpResult())
        .WithName("HandlePaymentWebhook")
        .AllowAnonymous()
        .WithSummary("IPN Webhook callback từ cổng thanh toán")
        .WithDescription("Receives asynchronous Instant Payment Notifications (IPN) from payment gateways (VNPay / PayPal / Stripe / Transfer) to update transaction and booking status.")
        .Produces<PaymentWebhookResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/{id:guid}/refund", async (
            Guid id,
            RefundPaymentRequest request,
            ClaimsPrincipal principal,
            IPaymentsApi api,
            CancellationToken ct) =>
            (await api.RefundPaymentAsync(
                id,
                GetUserId(principal),
                principal.IsInRole("Admin"),
                request.Amount,
                request.Reason,
                ct)).ToHttpResult())
        .WithValidation<RefundPaymentRequest>()
        .WithName("RefundPayment")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Hoàn tiền giao dịch thanh toán (toàn phần hoặc một phần)")
        .WithDescription("Processes a full or partial refund for a succeeded payment through the configured payment gateway.")
        .Produces<PaymentRefundDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/deposits/{depositId:guid}/release", async (
            Guid depositId,
            ReleaseDepositRequest? request,
            ClaimsPrincipal principal,
            IPaymentsApi api,
            CancellationToken ct) =>
            (await api.ReleaseDepositByIdAsync(
                depositId,
                GetUserId(principal),
                principal.IsInRole("Admin"),
                request?.Note,
                ct)).ToHttpResult())
        .WithName("ReleaseDeposit")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Hoàn trả toàn bộ tiền cọc sau khi trả xe")
        .WithDescription("Releases the full security deposit back to the customer after vehicle return inspection passes.")
        .Produces<DepositActionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/deposits/{depositId:guid}/deduct", async (
            Guid depositId,
            DeductDepositRequest request,
            ClaimsPrincipal principal,
            IPaymentsApi api,
            CancellationToken ct) =>
            (await api.DeductDepositByIdAsync(
                depositId,
                GetUserId(principal),
                principal.IsInRole("Admin"),
                request.DeductAmount,
                request.Reason,
                ct)).ToHttpResult())
        .WithValidation<DeductDepositRequest>()
        .WithName("DeductDeposit")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Khấu trừ cọc do hư hỏng, quá km, trễ hạn")
        .WithDescription("Deducts a specified amount from the security deposit for repair costs, over-mileage, or late returns, refunding the remainder.")
        .Produces<DepositActionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        return app;
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
    }
}
