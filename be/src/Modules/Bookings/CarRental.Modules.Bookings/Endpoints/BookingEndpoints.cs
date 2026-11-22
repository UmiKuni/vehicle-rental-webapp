using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Audit.Contracts;
using CarRental.Modules.Bookings.Contracts;
using CarRental.Modules.Bookings.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Bookings.Endpoints;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bookings")
            .WithTags("Bookings")
            .RequireAuthorization("AuthenticatedUser");

        // 1. Create a new booking request
        group.MapPost("/", async (
            CreateBookingRequest request,
            ClaimsPrincipal principal,
            IBookingApi api,
            CancellationToken ct) =>
            (await api.CreateAsync(GetUserId(principal), request, ct))
                .ToCreatedHttpResult(b => $"/api/bookings/{b.Id}"))
        .WithValidation<CreateBookingRequest>()
        .WithName("CreateBooking")
        .WithSummary("Tạo đơn đặt xe (Khóa lịch xe 12h, trạng thái Pending)")
        .WithDescription("Creates a new vehicle rental booking request with hold status, booking line items, and estimated charges.")
        .Produces<BookingDetailDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 2. Get customer's bookings (paginated) - supports both /me and /
        group.MapGet("/me", async (
            string? status,
            ClaimsPrincipal principal,
            IBookingApi api,
            CancellationToken ct,
            int page = 1,
            int pageSize = 10) =>
            (await api.GetMyBookingsAsync(GetUserId(principal), status, page, pageSize, ct)).ToHttpResult())
        .WithName("GetMyBookings")
        .WithSummary("Khách thuê xem danh sách đơn đã đặt")
        .WithDescription("Retrieves a paginated list of bookings created by the currently authenticated customer.")
        .Produces<PagedResult<BookingSummaryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapGet("/", async (
            string? status,
            ClaimsPrincipal principal,
            IBookingApi api,
            CancellationToken ct,
            int page = 1,
            int pageSize = 10) =>
            (await api.GetMyBookingsAsync(GetUserId(principal), status, page, pageSize, ct)).ToHttpResult())
        .WithName("GetAllMyBookings")
        .WithSummary("Khách thuê xem danh sách đơn đã đặt (alias)")
        .Produces<PagedResult<BookingSummaryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 3. Get owner's incoming booking requests (paginated)
        group.MapGet("/owner", async (
            string? status,
            ClaimsPrincipal principal,
            IBookingApi api,
            CancellationToken ct,
            int page = 1,
            int pageSize = 10) =>
            (await api.GetOwnerBookingsAsync(GetUserId(principal), status, page, pageSize, ct)).ToHttpResult())
        .WithName("GetOwnerBookings")
        .RequireAuthorization("OwnerOnly")
        .WithSummary("Chủ xe xem danh sách yêu cầu thuê xe")
        .WithDescription("Retrieves a paginated list of booking requests for vehicles owned by the authenticated owner.")
        .Produces<PagedResult<BookingSummaryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 4. Get booking detail by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IBookingApi api,
            CancellationToken ct) =>
            (await api.GetByIdAsync(id, GetUserId(principal), principal.IsInRole("Admin"), ct)).ToHttpResult())
        .WithName("GetBookingById")
        .WithSummary("Xem chi tiết đơn, dòng tiền & tiến trình")
        .WithDescription("Retrieves complete booking details including line item breakdown and status transition timeline.")
        .Produces<BookingDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 5. Approve booking request by owner
        group.MapPut("/{id:guid}/approve", async (
            Guid id,
            ClaimsPrincipal principal,
            IBookingApi api,
            CancellationToken ct) =>
            (await api.ApproveAsync(id, GetUserId(principal), ct)).ToHttpResult())
        .WithName("ApproveBooking")
        .RequireAuthorization("OwnerOnly")
        .WithSummary("Chủ xe phê duyệt đơn (sang Confirmed)")
        .WithDescription("Approves a pending booking request by the vehicle owner, moving the booking status to Confirmed.")
        .Produces<BookingDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 6. Reject booking request by owner
        group.MapPut("/{id:guid}/reject", async (
            Guid id,
            RejectBookingRequest request,
            ClaimsPrincipal principal,
            IBookingApi api,
            CancellationToken ct) =>
            (await api.RejectAsync(id, GetUserId(principal), request.Reason, ct)).ToHttpResult())
        .WithValidation<RejectBookingRequest>()
        .WithName("RejectBooking")
        .RequireAuthorization("OwnerOnly")
        .WithSummary("Chủ xe từ chối đơn đặt xe")
        .WithDescription("Rejects a pending booking request by the owner with a stated reason.")
        .Produces<BookingDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 7. Cancel booking by customer (applying policy BR-08)
        group.MapPut("/{id:guid}/cancel", async (
            Guid id,
            CancelBookingRequest request,
            ClaimsPrincipal principal,
            IBookingApi api,
            CancellationToken ct) =>
            (await api.CancelAsync(id, GetUserId(principal), request.Reason, ct)).ToHttpResult())
        .WithValidation<CancelBookingRequest>()
        .WithName("CancelBooking")
        .WithSummary("Khách hàng hủy đơn (tính phạt BR-08)")
        .WithDescription("Cancels a booking by the customer, applying platform cancellation policy BR-08 and computing penalties/refunds.")
        .Produces<BookingCancellationResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 8. Admin force cancellation
        group.MapPut("/{id:guid}/admin-cancel", async (
            Guid id,
            AdminCancelBookingRequest request,
            ClaimsPrincipal principal,
            IBookingApi api,
            IAuditRecorder auditRecorder,
            CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "AdminCancelBooking",
                entityName: "Booking",
                getEntityId: dto => dto.Id.ToString(),
                execute: () => api.AdminCancelAsync(id, adminId, request.Reason, request.PenaltyParty, ct),
                captureBeforeState: async () => (await api.GetRentalDetailsAsync(id, ct)).Value,
                captureAfterState: dto => new { dto.Status, dto.TotalRentAmount, dto.SecurityDepositAmount, Reason = request.Reason, PenaltyParty = request.PenaltyParty },
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
        .WithValidation<AdminCancelBookingRequest>()
        .WithName("AdminCancelBooking")
        .RequireAuthorization("AdminOnly")
        .WithSummary("Quản trị viên can thiệp hủy đơn")
        .WithDescription("Allows platform administrator to force-cancel a booking due to dispute, violation, or system event.")
        .Produces<BookingDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
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
