using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Bookings.Endpoints;

public static class BookingEndpoints
{
    public static IEndpointRouteBuilder MapBookingsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bookings")
            .WithTags("Bookings");

        // 1. Create a new booking request
        group.MapPost("/", (CreateBookingRequest request) =>
        {
            if (request.EndTime <= request.StartTime)
            {
                return Results.BadRequest(new { error = "EndTime must be greater than StartTime." });
            }

            var days = Math.Max(1, (int)Math.Ceiling((request.EndTime - request.StartTime).TotalDays));
            decimal dailyRate = 850_000m;
            decimal baseRent = days * dailyRate;
            decimal insuranceFee = request.IncludeInsurance ? days * 120_000m : 0m;
            decimal deposit = 5_000_000m;
            decimal totalRent = baseRent + insuranceFee;

            var bookingId = Guid.NewGuid();
            var lineItems = new List<BookingLineItemDto>
            {
                new(Guid.NewGuid(), "Rent", baseRent, $"Tiền thuê xe {days} ngày ({dailyRate:N0} VND/ngày)"),
                new(Guid.NewGuid(), "Deposit", deposit, "Tiền cọc giữ xe (hoàn trả 48h sau khi trả xe hoàn tất)")
            };

            if (request.IncludeInsurance)
            {
                lineItems.Insert(1, new(Guid.NewGuid(), "Insurance", insuranceFee, $"Bảo hiểm vật chất xe ({days} ngày)"));
            }

            var histories = new List<BookingStatusHistoryDto>
            {
                new("Pending", DateTimeOffset.UtcNow, "Khách hàng tạo yêu cầu thuê xe - Giữ lịch 12h chờ chủ xe phê duyệt")
            };

            var detail = new BookingDetailDto(
                Id: bookingId,
                CustomerId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CustomerName: "Nguyễn Văn An",
                CustomerPhone: "0912345678",
                OwnerId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                VehicleId: request.VehicleId,
                VehiclePlate: "51K-889.99",
                VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                Status: "Pending",
                StartTime: request.StartTime,
                EndTime: request.EndTime,
                PickupLocation: string.IsNullOrWhiteSpace(request.PickupLocation) ? "Quận 1, TP. Hồ Chí Minh" : request.PickupLocation,
                TotalRentAmount: totalRent,
                SecurityDepositAmount: deposit,
                HoldExpiresAt: DateTimeOffset.UtcNow.AddHours(12),
                CreatedAt: DateTimeOffset.UtcNow,
                LineItems: lineItems,
                StatusHistories: histories);

            return Results.Created($"/api/bookings/{bookingId}", detail);
        })
        .WithName("CreateBooking")
        .WithSummary("Tạo đơn đặt xe (Khóa lịch xe 12h, trạng thái Pending)")
        .WithDescription("Creates a new vehicle rental booking request with hold status, booking line items, and estimated charges.")
        .Produces<BookingDetailDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 2. Get customer's bookings (paginated)
        group.MapGet("/me", (string? status, int page = 1, int pageSize = 10) =>
        {
            var allBookings = new List<BookingSummaryDto>
            {
                new(
                    Id: Guid.Parse("a1111111-0000-0000-0000-000000000001"),
                    VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000001"),
                    VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                    VehiclePlate: "51K-889.99",
                    StartTime: DateTimeOffset.UtcNow.AddDays(2),
                    EndTime: DateTimeOffset.UtcNow.AddDays(5),
                    Status: "Confirmed",
                    TotalAmount: 2_550_000m,
                    CreatedAt: DateTimeOffset.UtcNow.AddDays(-1)),
                new(
                    Id: Guid.Parse("a1111111-0000-0000-0000-000000000002"),
                    VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000002"),
                    VehicleModel: "Honda CR-V 1.5L Turbo 2022",
                    VehiclePlate: "51H-123.45",
                    StartTime: DateTimeOffset.UtcNow.AddDays(-1),
                    EndTime: DateTimeOffset.UtcNow.AddDays(1),
                    Status: "InProgress",
                    TotalAmount: 2_200_000m,
                    CreatedAt: DateTimeOffset.UtcNow.AddDays(-3)),
                new(
                    Id: Guid.Parse("a1111111-0000-0000-0000-000000000003"),
                    VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000003"),
                    VehicleModel: "Toyota Camry 2.5Q 2021",
                    VehiclePlate: "51G-678.90",
                    StartTime: DateTimeOffset.UtcNow.AddDays(-10),
                    EndTime: DateTimeOffset.UtcNow.AddDays(-7),
                    Status: "Completed",
                    TotalAmount: 4_200_000m,
                    CreatedAt: DateTimeOffset.UtcNow.AddDays(-12)),
                new(
                    Id: Guid.Parse("a1111111-0000-0000-0000-000000000004"),
                    VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000004"),
                    VehicleModel: "Hyundai SantaFe 2.2 Dầu 2023",
                    VehiclePlate: "51F-555.55",
                    StartTime: DateTimeOffset.UtcNow.AddDays(7),
                    EndTime: DateTimeOffset.UtcNow.AddDays(10),
                    Status: "Pending",
                    TotalAmount: 3_900_000m,
                    CreatedAt: DateTimeOffset.UtcNow.AddHours(-2))
            };

            var filtered = string.IsNullOrWhiteSpace(status)
                ? allBookings
                : allBookings.Where(b => string.Equals(b.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();

            var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var result = new PagedResultDto<BookingSummaryDto>(paged, filtered.Count, page, pageSize);

            return Results.Ok(result);
        })
        .WithName("GetMyBookings")
        .WithSummary("Khách thuê xem danh sách đơn đã đặt")
        .WithDescription("Retrieves a paginated list of bookings created by the currently authenticated customer.")
        .Produces<PagedResultDto<BookingSummaryDto>>(StatusCodes.Status200OK);

        // 3. Get owner's incoming booking requests (paginated)
        group.MapGet("/owner", (string? status, int page = 1, int pageSize = 10) =>
        {
            var ownerBookings = new List<BookingSummaryDto>
            {
                new(
                    Id: Guid.Parse("b1111111-0000-0000-0000-000000000001"),
                    VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000001"),
                    VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                    VehiclePlate: "51K-889.99",
                    StartTime: DateTimeOffset.UtcNow.AddDays(1),
                    EndTime: DateTimeOffset.UtcNow.AddDays(4),
                    Status: "Pending",
                    TotalAmount: 2_550_000m,
                    CreatedAt: DateTimeOffset.UtcNow.AddHours(-1)),
                new(
                    Id: Guid.Parse("b1111111-0000-0000-0000-000000000002"),
                    VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000001"),
                    VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                    VehiclePlate: "51K-889.99",
                    StartTime: DateTimeOffset.UtcNow.AddDays(8),
                    EndTime: DateTimeOffset.UtcNow.AddDays(10),
                    Status: "Confirmed",
                    TotalAmount: 1_700_000m,
                    CreatedAt: DateTimeOffset.UtcNow.AddDays(-2)),
                new(
                    Id: Guid.Parse("b1111111-0000-0000-0000-000000000003"),
                    VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000001"),
                    VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                    VehiclePlate: "51K-889.99",
                    StartTime: DateTimeOffset.UtcNow.AddDays(-5),
                    EndTime: DateTimeOffset.UtcNow.AddDays(-2),
                    Status: "Completed",
                    TotalAmount: 2_550_000m,
                    CreatedAt: DateTimeOffset.UtcNow.AddDays(-7))
            };

            var filtered = string.IsNullOrWhiteSpace(status)
                ? ownerBookings
                : ownerBookings.Where(b => string.Equals(b.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();

            var paged = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            var result = new PagedResultDto<BookingSummaryDto>(paged, filtered.Count, page, pageSize);

            return Results.Ok(result);
        })
        .WithName("GetOwnerBookings")
        .WithSummary("Chủ xe xem danh sách yêu cầu thuê xe")
        .WithDescription("Retrieves a paginated list of booking requests for vehicles owned by the authenticated owner.")
        .Produces<PagedResultDto<BookingSummaryDto>>(StatusCodes.Status200OK);

        // 4. Get booking detail by ID
        group.MapGet("/{id:guid}", (Guid id) =>
        {
            var detail = new BookingDetailDto(
                Id: id,
                CustomerId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CustomerName: "Nguyễn Văn An",
                CustomerPhone: "0912345678",
                OwnerId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000001"),
                VehiclePlate: "51K-889.99",
                VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                Status: "Confirmed",
                StartTime: DateTimeOffset.UtcNow.AddDays(2),
                EndTime: DateTimeOffset.UtcNow.AddDays(5),
                PickupLocation: "285 Cách Mạng Tháng Tám, Phường 12, Quận 10, TP. Hồ Chí Minh",
                TotalRentAmount: 2_550_000m,
                SecurityDepositAmount: 5_000_000m,
                HoldExpiresAt: null,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-1),
                LineItems: new List<BookingLineItemDto>
                {
                    new(Guid.NewGuid(), "Rent", 2_550_000m, "Tiền thuê xe 3 ngày (850,000 VND/ngày)"),
                    new(Guid.NewGuid(), "Deposit", 5_000_000m, "Tiền thế chấp (hoàn lại sau chuyến đi)")
                },
                StatusHistories: new List<BookingStatusHistoryDto>
                {
                    new("Pending", DateTimeOffset.UtcNow.AddDays(-1), "Khách hàng tạo yêu cầu thuê xe"),
                    new("Confirmed", DateTimeOffset.UtcNow.AddHours(-18), "Chủ xe phê duyệt yêu cầu thuê xe"),
                    new("Paid", DateTimeOffset.UtcNow.AddHours(-16), "Khách hàng hoàn tất thanh toán cọc và tiền thuê qua VNPay")
                });

            return Results.Ok(detail);
        })
        .WithName("GetBookingById")
        .WithSummary("Xem chi tiết đơn, dòng tiền & tiến trình")
        .WithDescription("Retrieves complete booking details including line item breakdown and status transition timeline.")
        .Produces<BookingDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 5. Approve booking request by owner
        group.MapPut("/{id:guid}/approve", (Guid id) =>
        {
            var approvedBooking = new BookingDetailDto(
                Id: id,
                CustomerId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CustomerName: "Nguyễn Văn An",
                CustomerPhone: "0912345678",
                OwnerId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000001"),
                VehiclePlate: "51K-889.99",
                VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                Status: "Confirmed",
                StartTime: DateTimeOffset.UtcNow.AddDays(2),
                EndTime: DateTimeOffset.UtcNow.AddDays(5),
                PickupLocation: "Quận 10, TP. Hồ Chí Minh",
                TotalRentAmount: 2_550_000m,
                SecurityDepositAmount: 5_000_000m,
                HoldExpiresAt: null,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-1),
                LineItems: new List<BookingLineItemDto>
                {
                    new(Guid.NewGuid(), "Rent", 2_550_000m, "Tiền thuê xe 3 ngày (850,000 VND/ngày)"),
                    new(Guid.NewGuid(), "Deposit", 5_000_000m, "Tiền cọc giữ xe")
                },
                StatusHistories: new List<BookingStatusHistoryDto>
                {
                    new("Pending", DateTimeOffset.UtcNow.AddDays(-1), "Khách hàng tạo yêu cầu thuê xe"),
                    new("Confirmed", DateTimeOffset.UtcNow, "Chủ xe đã phê duyệt đơn đặt xe")
                });

            return Results.Ok(approvedBooking);
        })
        .WithName("ApproveBooking")
        .WithSummary("Chủ xe phê duyệt đơn (sang Confirmed)")
        .WithDescription("Approves a pending booking request by the vehicle owner, moving the booking status to Confirmed.")
        .Produces<BookingDetailDto>(StatusCodes.Status200OK);

        // 6. Reject booking request by owner
        group.MapPut("/{id:guid}/reject", (Guid id, RejectBookingRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "Reason is required to reject a booking." });
            }

            var rejectedBooking = new BookingDetailDto(
                Id: id,
                CustomerId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CustomerName: "Nguyễn Văn An",
                CustomerPhone: "0912345678",
                OwnerId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000001"),
                VehiclePlate: "51K-889.99",
                VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                Status: "Rejected",
                StartTime: DateTimeOffset.UtcNow.AddDays(2),
                EndTime: DateTimeOffset.UtcNow.AddDays(5),
                PickupLocation: "Quận 10, TP. Hồ Chí Minh",
                TotalRentAmount: 2_550_000m,
                SecurityDepositAmount: 5_000_000m,
                HoldExpiresAt: null,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-1),
                LineItems: [],
                StatusHistories: new List<BookingStatusHistoryDto>
                {
                    new("Pending", DateTimeOffset.UtcNow.AddDays(-1), "Khách hàng tạo yêu cầu thuê xe"),
                    new("Rejected", DateTimeOffset.UtcNow, $"Chủ xe từ chối đơn: {request.Reason}")
                });

            return Results.Ok(rejectedBooking);
        })
        .WithName("RejectBooking")
        .WithSummary("Chủ xe từ chối đơn đặt xe")
        .WithDescription("Rejects a pending booking request by the owner with a stated reason.")
        .Produces<BookingDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 7. Cancel booking by customer (applying policy BR-08)
        group.MapPut("/{id:guid}/cancel", (Guid id, CancelBookingRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "Reason is required to cancel a booking." });
            }

            var cancellationResult = new BookingCancellationResultDto(
                BookingId: id,
                Status: "Cancelled",
                TotalPaid: 3_200_000m,
                PenaltyAmount: 500_000m,
                RefundAmount: 2_700_000m,
                CancellationReason: request.Reason,
                CancelledAt: DateTimeOffset.UtcNow);

            return Results.Ok(cancellationResult);
        })
        .WithName("CancelBooking")
        .WithSummary("Khách hàng hủy đơn (tính phạt BR-08)")
        .WithDescription("Cancels a booking by the customer, applying platform cancellation policy BR-08 and computing penalties/refunds.")
        .Produces<BookingCancellationResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 8. Admin force cancellation
        group.MapPut("/{id:guid}/admin-cancel", (Guid id, AdminCancelBookingRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "Reason is required for admin cancellation." });
            }

            var adminCancelledBooking = new BookingDetailDto(
                Id: id,
                CustomerId: Guid.Parse("11111111-1111-1111-1111-111111111111"),
                CustomerName: "Nguyễn Văn An",
                CustomerPhone: "0912345678",
                OwnerId: Guid.Parse("22222222-2222-2222-2222-222222222222"),
                VehicleId: Guid.Parse("d1111111-0000-0000-0000-000000000001"),
                VehiclePlate: "51K-889.99",
                VehicleModel: "Mazda CX-5 2.0 Premium 2023",
                Status: "Cancelled",
                StartTime: DateTimeOffset.UtcNow.AddDays(2),
                EndTime: DateTimeOffset.UtcNow.AddDays(5),
                PickupLocation: "Quận 10, TP. Hồ Chí Minh",
                TotalRentAmount: 2_550_000m,
                SecurityDepositAmount: 5_000_000m,
                HoldExpiresAt: null,
                CreatedAt: DateTimeOffset.UtcNow.AddDays(-1),
                LineItems: [],
                StatusHistories: new List<BookingStatusHistoryDto>
                {
                    new("Pending", DateTimeOffset.UtcNow.AddDays(-1), "Khách hàng tạo yêu cầu thuê xe"),
                    new("Cancelled", DateTimeOffset.UtcNow, $"Quản trị viên can thiệp hủy đơn: {request.Reason} (Bên chịu chế tài: {request.PenaltyParty})")
                });

            return Results.Ok(adminCancelledBooking);
        })
        .WithName("AdminCancelBooking")
        .WithSummary("Quản trị viên can thiệp hủy đơn")
        .WithDescription("Allows platform administrator to force-cancel a booking due to dispute, violation, or system event.")
        .Produces<BookingDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}

public sealed record CreateBookingRequest(
    Guid VehicleId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string PickupLocation,
    bool IncludeInsurance,
    string? Notes);

public sealed record BookingLineItemDto(
    Guid Id,
    string Type, // Rent, Deposit, CleaningFee, OverKmFee
    decimal Amount,
    string Description);

public sealed record BookingStatusHistoryDto(
    string Status,
    DateTimeOffset ChangedAt,
    string? Reason);

public sealed record BookingDetailDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string CustomerPhone,
    Guid OwnerId,
    Guid VehicleId,
    string VehiclePlate,
    string VehicleModel,
    string Status, // Pending, Confirmed, Paid, InProgress, Completed, Cancelled, Rejected, Expired
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string PickupLocation,
    decimal TotalRentAmount,
    decimal SecurityDepositAmount,
    DateTimeOffset? HoldExpiresAt,
    DateTimeOffset CreatedAt,
    IReadOnlyList<BookingLineItemDto> LineItems,
    IReadOnlyList<BookingStatusHistoryDto> StatusHistories);

public sealed record BookingSummaryDto(
    Guid Id,
    Guid VehicleId,
    string VehicleModel,
    string VehiclePlate,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Status,
    decimal TotalAmount,
    DateTimeOffset CreatedAt);

public sealed record RejectBookingRequest(string Reason);
public sealed record CancelBookingRequest(string Reason);
public sealed record AdminCancelBookingRequest(string Reason, string PenaltyParty);

public sealed record BookingCancellationResultDto(
    Guid BookingId,
    string Status,
    decimal TotalPaid,
    decimal PenaltyAmount,
    decimal RefundAmount,
    string CancellationReason,
    DateTimeOffset CancelledAt);

public sealed record PagedResultDto<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize);
