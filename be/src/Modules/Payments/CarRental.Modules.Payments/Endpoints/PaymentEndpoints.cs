using System.Security.Claims;
using System.Text;
using CarRental.BuildingBlocks.Results;
using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Payments.Contracts;
using CarRental.Modules.Payments.Contracts.Dtos;
using CarRental.Modules.Payments.Contracts.Enums;
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

        group.MapGet("/webhooks/vnpay", async (
            HttpRequest httpRequest,
            IPaymentsApi api,
            CancellationToken ct) =>
        {
            var headers = ExtractHeaders(httpRequest);
            var query = ExtractQuery(httpRequest);
            var result = await api.HandleGatewayWebhookAsync(
                new GatewayWebhookRequest(PaymentMethod.VNPay, string.Empty, headers, query),
                ct);

            if (result.IsSuccess)
            {
                return string.Equals(result.Value.Message, "Webhook already processed.", StringComparison.Ordinal)
                    ? Results.Ok(new VnPayIpnResponseDto("02", "Order already confirmed"))
                    : Results.Ok(new VnPayIpnResponseDto("00", "Confirm Success"));
            }

            var (rspCode, message) = result.Error.Code switch
            {
                "payment.invalid_signature" => ("97", "Invalid signature"),
                "payment.amount_mismatch" => ("04", "Invalid amount"),
                "payment.not_found" or "payment.invalid_order_id" => ("01", "Order not found"),
                "payment.invalid_state" => ("02", "Order already confirmed"),
                _ => ("99", result.Error.Message)
            };

            return Results.Ok(new VnPayIpnResponseDto(rspCode, message));
        })
        .WithName("HandleVnPayIpn")
        .AllowAnonymous()
        .WithSummary("VNPay IPN callback")
        .WithDescription("Receives VNPay Instant Payment Notification (GET query string signed with HMAC-SHA512) and returns official VNPay RspCode JSON.")
        .Produces<VnPayIpnResponseDto>(StatusCodes.Status200OK);

        group.MapPost("/webhooks/stripe", (
            HttpRequest httpRequest,
            IPaymentsApi api,
            CancellationToken ct) =>
            HandleRawBodyWebhookAsync(httpRequest, PaymentMethod.Stripe, api, ct))
        .WithName("HandleStripeWebhook")
        .AllowAnonymous()
        .WithSummary("Stripe Webhook callback")
        .WithDescription("Receives Stripe webhook events verified via Stripe-Signature HMAC-SHA256 against the raw request body.")
        .Produces<PaymentWebhookResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/webhooks/paypal", (
            HttpRequest httpRequest,
            IPaymentsApi api,
            CancellationToken ct) =>
            HandleRawBodyWebhookAsync(httpRequest, PaymentMethod.Paypal, api, ct))
        .WithName("HandlePayPalWebhook")
        .AllowAnonymous()
        .WithSummary("PayPal Webhook callback")
        .WithDescription("Receives PayPal webhook events verified via PayPal Verify Webhook Signature API.")
        .Produces<PaymentWebhookResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/webhooks/transfer", (
            HttpRequest httpRequest,
            IPaymentsApi api,
            CancellationToken ct) =>
            HandleRawBodyWebhookAsync(httpRequest, PaymentMethod.Transfer, api, ct))
        .WithName("HandleBankTransferWebhook")
        .AllowAnonymous()
        .WithSummary("Bank Transfer / VietQR Webhook callback")
        .WithDescription("Receives inbound bank transfer notifications (SePay/Casso/VietQR) verified via API key or HMAC-SHA256 signature.")
        .Produces<PaymentWebhookResponseDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/paypal/capture", async (
            CapturePayPalOrderRequest request,
            ClaimsPrincipal principal,
            IPaymentsApi api,
            CancellationToken ct) =>
            (await api.CapturePayPalOrderAsync(request.OrderId, GetUserId(principal), principal.IsInRole("Admin"), ct)).ToHttpResult())
        .WithValidation<CapturePayPalOrderRequest>()
        .WithName("CapturePayPalOrder")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Xác nhận & capture đơn PayPal sau khi người dùng duyệt thanh toán")
        .WithDescription("Captures an approved PayPal order after customer redirect and finalizes payment and booking status.")
        .Produces<PaymentDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
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

    private static async Task<IResult> HandleRawBodyWebhookAsync(
        HttpRequest httpRequest,
        PaymentMethod method,
        IPaymentsApi api,
        CancellationToken ct)
    {
        using var reader = new StreamReader(httpRequest.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var rawBody = await reader.ReadToEndAsync(ct);
        var headers = ExtractHeaders(httpRequest);
        var query = ExtractQuery(httpRequest);

        return (await api.HandleGatewayWebhookAsync(
            new GatewayWebhookRequest(method, rawBody, headers, query),
            ct)).ToHttpResult();
    }

    private static Dictionary<string, string> ExtractHeaders(HttpRequest request)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value.ToArray());
        }

        return headers;
    }

    private static Dictionary<string, string> ExtractQuery(HttpRequest request)
    {
        var query = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in request.Query)
        {
            query[pair.Key] = pair.Value.ToString();
        }

        return query;
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
    }
}
