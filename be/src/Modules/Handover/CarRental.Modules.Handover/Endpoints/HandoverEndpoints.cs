using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Handover.Endpoints;

public static class HandoverEndpoints
{
    public static IEndpointRouteBuilder MapHandoverEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/handovers")
            .WithTags("Handover");

        // 1. Create a new handover inspection record
        group.MapPost("/", (CreateHandoverRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Type) ||
                (!string.Equals(request.Type, "Pickup", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(request.Type, "Return", StringComparison.OrdinalIgnoreCase)))
            {
                return Results.BadRequest(new { error = "Type must be either 'Pickup' or 'Return'." });
            }

            var handoverId = Guid.NewGuid();
            var condition = new VehicleConditionDto(
                HandoverId: handoverId,
                Odo: 15420,
                FuelLevel: 100,
                DamageNote: null);

            var detail = new HandoverDetailDto(
                Id: handoverId,
                BookingId: request.BookingId,
                Type: request.Type,
                Status: "Draft",
                ServerReceivedAt: DateTimeOffset.UtcNow,
                Condition: condition,
                Photos: [],
                IsConfirmedByOwner: false,
                IsConfirmedByCustomer: false,
                FinalizedAt: null);

            return Results.Created($"/api/handovers/{handoverId}", detail);
        })
        .WithName("CreateHandover")
        .WithSummary("Tạo biên bản giao nhận xe (Pickup / Return)")
        .WithDescription("Initializes a vehicle handover protocol draft for vehicle pickup or return.")
        .Produces<HandoverDetailDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 2. Get all handovers for a booking
        group.MapGet("/booking/{bookingId:guid}", (Guid bookingId) =>
        {
            var handovers = new[]
            {
                new HandoverSummaryDto(
                    Id: Guid.Parse("c1111111-0000-0000-0000-000000000001"),
                    BookingId: bookingId,
                    Type: "Pickup",
                    Status: "Confirmed",
                    ServerReceivedAt: DateTimeOffset.UtcNow.AddDays(-3)),
                new HandoverSummaryDto(
                    Id: Guid.Parse("c1111111-0000-0000-0000-000000000002"),
                    BookingId: bookingId,
                    Type: "Return",
                    Status: "Draft",
                    ServerReceivedAt: DateTimeOffset.UtcNow)
            };

            return Results.Ok(handovers);
        })
        .WithName("GetHandoversByBooking")
        .WithSummary("Xem danh sách biên bản giao & nhận của chuyến")
        .WithDescription("Retrieves both pickup and return handover records for a specific booking.")
        .Produces<HandoverSummaryDto[]>(StatusCodes.Status200OK);

        // 3. Get handover detail by ID
        group.MapGet("/{id:guid}", (Guid id) =>
        {
            var photos = new List<HandoverPhotoDto>
            {
                new(Guid.NewGuid(), id, "FrontLeft", "photos/2026/10/fl_preview.jpg", "e3b0c44298fc1c149afbf4c8996fb924", "https://storage.carrental.vn/handovers/fl_preview.jpg", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(-1)),
                new(Guid.NewGuid(), id, "FrontRight", "photos/2026/10/fr_preview.jpg", "c4ca4238a0b923820dcc509a6f75849b", "https://storage.carrental.vn/handovers/fr_preview.jpg", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(-1)),
                new(Guid.NewGuid(), id, "BackLeft", "photos/2026/10/bl_preview.jpg", "c81e728d9d4c2f636f067f89cc14862c", "https://storage.carrental.vn/handovers/bl_preview.jpg", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(-1)),
                new(Guid.NewGuid(), id, "BackRight", "photos/2026/10/br_preview.jpg", "eccbc87e4b5ce2fe28308fd9f2a7baf3", "https://storage.carrental.vn/handovers/br_preview.jpg", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(-1)),
                new(Guid.NewGuid(), id, "Dash", "photos/2026/10/dash_preview.jpg", "a87ff679a2f3e71d9181a67b7542122c", "https://storage.carrental.vn/handovers/dash_preview.jpg", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(-1)),
                new(Guid.NewGuid(), id, "Trunk", "photos/2026/10/trunk_preview.jpg", "e4da3b7fbbce2345d7772b0674a318d5", "https://storage.carrental.vn/handovers/trunk_preview.jpg", DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(-1))
            };

            var detail = new HandoverDetailDto(
                Id: id,
                BookingId: Guid.Parse("a1111111-0000-0000-0000-000000000001"),
                Type: "Pickup",
                Status: "Confirmed",
                ServerReceivedAt: DateTimeOffset.UtcNow.AddDays(-3),
                Condition: new VehicleConditionDto(
                    HandoverId: id,
                    Odo: 15420,
                    FuelLevel: 100,
                    DamageNote: "Xe sạch sẽ, có vết xước nhẹ ở cản trước góc phải"),
                Photos: photos,
                IsConfirmedByOwner: true,
                IsConfirmedByCustomer: true,
                FinalizedAt: DateTimeOffset.UtcNow.AddDays(-3).AddMinutes(15));

            return Results.Ok(detail);
        })
        .WithName("GetHandoverById")
        .WithSummary("Xem chi tiết biên bản, ảnh 4 góc & Odo")
        .WithDescription("Retrieves full details of a vehicle handover report, including odometer, fuel level, corner photos, and digital signatures.")
        .Produces<HandoverDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        // 4. Update vehicle condition (odo, fuel level, damage note)
        group.MapPut("/{id:guid}/condition", (Guid id, UpdateVehicleConditionRequest request) =>
        {
            if (request.Odo < 0 || request.FuelLevel < 0 || request.FuelLevel > 100)
            {
                return Results.BadRequest(new { error = "Odo must be non-negative and FuelLevel must be between 0 and 100." });
            }

            var condition = new VehicleConditionDto(
                HandoverId: id,
                Odo: request.Odo,
                FuelLevel: request.FuelLevel,
                DamageNote: request.DamageNote);

            return Results.Ok(condition);
        })
        .WithName("UpdateVehicleCondition")
        .WithSummary("Cập nhật số Km Odo, % nhiên liệu & hư hại")
        .WithDescription("Updates vehicle inspection metrics such as current odometer reading, fuel percentage, and noted damages.")
        .Produces<VehicleConditionDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 5. Upload vehicle handover photo
        group.MapPost("/{id:guid}/photos", (Guid id, UploadHandoverPhotoRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.Corner) || string.IsNullOrWhiteSpace(request.StorageKey))
            {
                return Results.BadRequest(new { error = "Corner and StorageKey are required." });
            }

            var photo = new HandoverPhotoDto(
                Id: Guid.NewGuid(),
                HandoverId: id,
                Corner: request.Corner,
                StorageKey: request.StorageKey,
                FileHash: request.FileHash,
                ViewUrl: $"https://storage.carrental.vn/handovers/{request.StorageKey}",
                ClientCapturedAt: request.ClientCapturedAt,
                CreatedAt: DateTimeOffset.UtcNow);

            return Results.Created($"/api/handovers/{id}/photos/{photo.Id}", photo);
        })
        .WithName("UploadHandoverPhoto")
        .WithSummary("Ghi nhận ảnh góc xe, đồng hồ Odo")
        .WithDescription("Registers an inspected vehicle photo (FrontLeft, Dash, Trunk, etc.) with metadata and storage key.")
        .Produces<HandoverPhotoDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 6. Confirm handover with digital signature
        group.MapPut("/{id:guid}/confirm", (Guid id, ConfirmHandoverRequest request) =>
        {
            if (string.IsNullOrWhiteSpace(request.ConfirmedByRole) ||
                (!string.Equals(request.ConfirmedByRole, "Customer", StringComparison.OrdinalIgnoreCase) &&
                 !string.Equals(request.ConfirmedByRole, "Owner", StringComparison.OrdinalIgnoreCase)))
            {
                return Results.BadRequest(new { error = "ConfirmedByRole must be either 'Customer' or 'Owner'." });
            }

            bool isCustomer = string.Equals(request.ConfirmedByRole, "Customer", StringComparison.OrdinalIgnoreCase);

            var finalizedDetail = new HandoverDetailDto(
                Id: id,
                BookingId: Guid.Parse("a1111111-0000-0000-0000-000000000001"),
                Type: "Return",
                Status: "Confirmed",
                ServerReceivedAt: DateTimeOffset.UtcNow.AddMinutes(-10),
                Condition: new VehicleConditionDto(id, 15720, 95, "Xe bàn giao đúng tình trạng ban đầu"),
                Photos: [],
                IsConfirmedByOwner: true,
                IsConfirmedByCustomer: true,
                FinalizedAt: DateTimeOffset.UtcNow);

            return Results.Ok(finalizedDetail);
        })
        .WithName("ConfirmHandover")
        .WithSummary("Chủ xe & Khách ký chốt (bất biến dữ liệu BR-13)")
        .WithDescription("Submits digital signature confirmation from customer or owner. Once both parties confirm, handover becomes immutable per BR-13.")
        .Produces<HandoverDetailDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        return app;
    }
}

public sealed record CreateHandoverRequest(
    Guid BookingId,
    string Type); // "Pickup" hoặc "Return"

public sealed record UpdateVehicleConditionRequest(
    int Odo,
    int FuelLevel, // 0 - 100%
    string? DamageNote);

public sealed record VehicleConditionDto(
    Guid HandoverId,
    int Odo,
    int FuelLevel,
    string? DamageNote);

public sealed record UploadHandoverPhotoRequest(
    string Corner, // FrontLeft, FrontRight, BackLeft, BackRight, Dash, Trunk, Interior
    string StorageKey,
    string FileHash,
    DateTimeOffset ClientCapturedAt);

public sealed record HandoverPhotoDto(
    Guid Id,
    Guid HandoverId,
    string Corner,
    string StorageKey,
    string FileHash,
    string ViewUrl,
    DateTimeOffset ClientCapturedAt,
    DateTimeOffset CreatedAt);

public sealed record ConfirmHandoverRequest(
    string ConfirmedByRole, // "Customer" hoặc "Owner"
    string? SignatureToken);

public sealed record HandoverDetailDto(
    Guid Id,
    Guid BookingId,
    string Type, // Pickup, Return
    string Status, // Draft, Confirmed
    DateTimeOffset ServerReceivedAt,
    VehicleConditionDto? Condition,
    IReadOnlyList<HandoverPhotoDto> Photos,
    bool IsConfirmedByOwner,
    bool IsConfirmedByCustomer,
    DateTimeOffset? FinalizedAt);

public sealed record HandoverSummaryDto(
    Guid Id,
    Guid BookingId,
    string Type,
    string Status,
    DateTimeOffset ServerReceivedAt);
