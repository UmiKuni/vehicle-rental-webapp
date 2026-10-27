using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Availability.Endpoints;

public static class AvailabilityEndpoints
{
    public static IEndpointRouteBuilder MapAvailabilityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/availability")
            .WithTags("Availability");

        // 1. Check vehicle availability for a given date range
        group.MapGet("/vehicles/{vehicleId:guid}/check", (Guid vehicleId, DateTimeOffset? start, DateTimeOffset? end) =>
        {
            if (!start.HasValue || !end.HasValue || end.Value <= start.Value)
            {
                return Results.BadRequest(new { error = "Valid start and end query parameters are required, and end must be greater than start." });
            }

            // Realistic mock busy periods (e.g. existing confirmed booking)
            var busyPeriods = new List<DateTimeRangeDto>
            {
                new(
                    Start: DateTimeOffset.UtcNow.AddDays(3).Date.AddHours(8),
                    End: DateTimeOffset.UtcNow.AddDays(5).Date.AddHours(20))
            };

            var hasConflict = busyPeriods.Any(p => start.Value < p.End && end.Value > p.Start);

            var response = new AvailabilityCheckResultDto(
                VehicleId: vehicleId,
                StartTime: start.Value,
                EndTime: end.Value,
                IsAvailable: !hasConflict,
                ConflictReason: hasConflict ? "Xe đã có lịch đặt hoặc đang được giữ chỗ trong khoảng thời gian này." : null,
                BusyPeriods: hasConflict ? busyPeriods : []);

            return Results.Ok(response);
        })
        .WithName("CheckVehicleAvailability")
        .WithSummary("Check vehicle availability for a given date range")
        .WithDescription("Checks whether a vehicle is free to be rented within the requested time range or collides with existing bookings/holds/blocks.")
        .Produces<AvailabilityCheckResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 2. Get vehicle calendar and blocked schedule entries
        group.MapGet("/vehicles/{vehicleId:guid}/calendar", (Guid vehicleId, DateTimeOffset? from, DateTimeOffset? to) =>
        {
            var entries = new[]
            {
                new CalendarEntryDto(
                    Id: Guid.NewGuid(),
                    VehicleId: vehicleId,
                    Type: "Booked",
                    StartTime: DateTimeOffset.UtcNow.AddDays(3).Date.AddHours(8),
                    EndTime: DateTimeOffset.UtcNow.AddDays(5).Date.AddHours(20),
                    ExpiresAt: null,
                    BookingId: Guid.NewGuid(),
                    Note: "Đơn thuê đã xác nhận #BK-9182"),
                new CalendarEntryDto(
                    Id: Guid.NewGuid(),
                    VehicleId: vehicleId,
                    Type: "Hold",
                    StartTime: DateTimeOffset.UtcNow.AddDays(7).Date.AddHours(9),
                    EndTime: DateTimeOffset.UtcNow.AddDays(8).Date.AddHours(18),
                    ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(12),
                    BookingId: Guid.NewGuid(),
                    Note: "Khách hàng đang thanh toán cọc"),
                new CalendarEntryDto(
                    Id: Guid.NewGuid(),
                    VehicleId: vehicleId,
                    Type: "OwnerBlocked",
                    StartTime: DateTimeOffset.UtcNow.AddDays(12).Date.AddHours(0),
                    EndTime: DateTimeOffset.UtcNow.AddDays(14).Date.AddHours(23).AddMinutes(59),
                    ExpiresAt: null,
                    BookingId: null,
                    Note: "Chủ xe sử dụng cá nhân / về quê"),
                new CalendarEntryDto(
                    Id: Guid.NewGuid(),
                    VehicleId: vehicleId,
                    Type: "Maintenance",
                    StartTime: DateTimeOffset.UtcNow.AddDays(20).Date.AddHours(8),
                    EndTime: DateTimeOffset.UtcNow.AddDays(21).Date.AddHours(17),
                    ExpiresAt: null,
                    BookingId: null,
                    Note: "Bảo dưỡng định kỳ 20.000 km và thay dầu")
            };

            return Results.Ok(entries);
        })
        .WithName("GetVehicleCalendar")
        .WithSummary("Get vehicle calendar and blocked schedule entries")
        .WithDescription("Retrieves all booked periods, temporary holds, maintenance blocks, and owner-blocked dates for a vehicle within a date range.")
        .Produces<CalendarEntryDto[]>(StatusCodes.Status200OK);

        // 3. Block vehicle schedule manually
        group.MapPost("/vehicles/{vehicleId:guid}/blocks", (Guid vehicleId, BlockScheduleRequest request) =>
        {
            if (request.EndTime <= request.StartTime || string.IsNullOrWhiteSpace(request.Reason))
            {
                return Results.BadRequest(new { error = "EndTime must be greater than StartTime, and Reason is required." });
            }

            var entry = new CalendarEntryDto(
                Id: Guid.NewGuid(),
                VehicleId: vehicleId,
                Type: "OwnerBlocked",
                StartTime: request.StartTime,
                EndTime: request.EndTime,
                ExpiresAt: null,
                BookingId: null,
                Note: request.Reason);

            return Results.Created($"/api/availability/vehicles/{vehicleId}/calendar", entry);
        })
        .WithName("BlockVehicleSchedule")
        .WithSummary("Block vehicle schedule manually")
        .WithDescription("Allows vehicle owners to manually block dates for personal use or scheduled maintenance.")
        .Produces<CalendarEntryDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        // 4. Unblock previously blocked schedule
        group.MapDelete("/blocks/{blockId:guid}", (Guid blockId) =>
        {
            return Results.NoContent();
        })
        .WithName("UnblockVehicleSchedule")
        .WithSummary("Unblock previously blocked schedule")
        .WithDescription("Removes an owner-created block, restoring vehicle availability for bookings.")
        .Produces(StatusCodes.Status204NoContent);

        return app;
    }
}

public sealed record AvailabilityCheckResultDto(
    Guid VehicleId,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    bool IsAvailable,
    string? ConflictReason,
    IReadOnlyList<DateTimeRangeDto> BusyPeriods);

public sealed record DateTimeRangeDto(
    DateTimeOffset Start,
    DateTimeOffset End);

public sealed record CalendarEntryDto(
    Guid Id,
    Guid VehicleId,
    string Type, // Booked, Hold, OwnerBlocked, Maintenance
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    DateTimeOffset? ExpiresAt,
    Guid? BookingId,
    string? Note);

public sealed record BlockScheduleRequest(
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    string Reason);
