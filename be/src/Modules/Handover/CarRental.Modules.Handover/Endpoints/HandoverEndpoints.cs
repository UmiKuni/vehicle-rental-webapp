using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Handover.Contracts;
using CarRental.Modules.Handover.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Handover.Endpoints;

public static class HandoverEndpoints
{
    public static IEndpointRouteBuilder MapHandoverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/handovers")
            .WithTags("Handover")
            .RequireAuthorization("AuthenticatedUser");

        // 1. Create a new handover inspection record
        group.MapPost("/", async (
            CreateHandoverRequest request,
            ClaimsPrincipal principal,
            IHandoverApi api,
            CancellationToken ct) =>
            (await api.CreateAsync(GetUserId(principal), request, ct))
                .ToCreatedHttpResult(dto => $"/api/handovers/{dto.Id}"))
        .WithValidation<CreateHandoverRequest>()
        .WithName("CreateHandover")
        .WithSummary("Tạo biên bản giao nhận xe (Pickup / Return)")
        .WithDescription("Initializes a vehicle handover protocol draft for vehicle pickup or return.")
        .Produces<HandoverDetailDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 2. Get all handovers for a booking
        group.MapGet("/booking/{bookingId:guid}", async (
            Guid bookingId,
            ClaimsPrincipal principal,
            IHandoverApi api,
            CancellationToken ct) =>
            (await api.GetByBookingAsync(
                bookingId,
                GetUserId(principal),
                principal.IsInRole("Admin"),
                ct)).ToHttpResult())
        .WithName("GetHandoversByBooking")
        .WithSummary("Xem danh sách biên bản giao & nhận của chuyến")
        .WithDescription("Retrieves both pickup and return handover records for a specific booking.")
        .Produces<IReadOnlyList<HandoverSummaryDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // 3. Get handover detail by ID
        group.MapGet("/{id:guid}", async (
            Guid id,
            ClaimsPrincipal principal,
            IHandoverApi api,
            CancellationToken ct) =>
            (await api.GetByIdAsync(
                id,
                GetUserId(principal),
                principal.IsInRole("Admin"),
                ct)).ToHttpResult())
        .WithName("GetHandoverById")
        .WithSummary("Xem chi tiết biên bản, ảnh 4 góc & Odo")
        .WithDescription("Retrieves full details of a vehicle handover report, including odometer, fuel level, corner photos, and digital signatures.")
        .Produces<HandoverDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 4. Update vehicle condition (odo, fuel level, damage note)
        group.MapPut("/{id:guid}/condition", async (
            Guid id,
            UpdateVehicleConditionRequest request,
            ClaimsPrincipal principal,
            IHandoverApi api,
            CancellationToken ct) =>
            (await api.UpdateConditionAsync(id, GetUserId(principal), request, ct)).ToHttpResult())
        .WithValidation<UpdateVehicleConditionRequest>()
        .WithName("UpdateVehicleCondition")
        .WithSummary("Cập nhật số Km Odo, % nhiên liệu & hư hại")
        .WithDescription("Updates vehicle inspection metrics such as current odometer reading, fuel percentage, and noted damages.")
        .Produces<VehicleConditionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 5. Upload vehicle handover photo
        group.MapPost("/{id:guid}/photos", async (
            Guid id,
            UploadHandoverPhotoRequest request,
            ClaimsPrincipal principal,
            IHandoverApi api,
            CancellationToken ct) =>
            (await api.AddPhotoAsync(id, GetUserId(principal), request, ct))
                .ToCreatedHttpResult(dto => $"/api/handovers/{id}/photos/{dto.Id}"))
        .WithValidation<UploadHandoverPhotoRequest>()
        .WithName("UploadHandoverPhoto")
        .WithSummary("Ghi nhận ảnh góc xe, đồng hồ Odo")
        .WithDescription("Registers an inspected vehicle photo (FrontLeft, FrontRight, RearLeft, RearRight, Dash) with metadata and storage key.")
        .Produces<HandoverPhotoDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        // 6. Confirm handover with digital signature
        group.MapPut("/{id:guid}/confirm", async (
            Guid id,
            ConfirmHandoverRequest request,
            ClaimsPrincipal principal,
            IHandoverApi api,
            CancellationToken ct) =>
            (await api.ConfirmAsync(id, GetUserId(principal), request, ct)).ToHttpResult())
        .WithValidation<ConfirmHandoverRequest>()
        .WithName("ConfirmHandover")
        .WithSummary("Chủ xe & Khách ký chốt (bất biến dữ liệu BR-13)")
        .WithDescription("Submits digital signature confirmation from customer or owner. Once both parties confirm, handover becomes immutable per BR-13.")
        .Produces<HandoverDetailDto>(StatusCodes.Status200OK)
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
