using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using CarRental.BuildingBlocks.Results;
using CarRental.BuildingBlocks.Validation;
using CarRental.Modules.Handover.Contracts;
using CarRental.Modules.Handover.Contracts.Dtos;
using FluentValidation;
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

        // 5. Upload vehicle handover photo (supports multipart/form-data file upload and application/json metadata)
        group.MapPost("/{id:guid}/photos", async (
            Guid id,
            HttpContext httpContext,
            ClaimsPrincipal principal,
            IHandoverApi api,
            IValidator<UploadHandoverPhotoRequest> jsonValidator,
            CancellationToken ct) =>
        {
            var actorId = GetUserId(principal);

            if (httpContext.Request.HasFormContentType)
            {
                var form = await httpContext.Request.ReadFormAsync(ct);
                var corner = form["corner"].ToString();
                var capturedAtRaw = form["clientCapturedAt"].ToString();
                DateTimeOffset? capturedAt = DateTimeOffset.TryParse(
                    capturedAtRaw,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var parsedAt)
                    ? parsedAt
                    : null;

                var file = form.Files.GetFile("file") ?? form.Files.FirstOrDefault();
                if (file is null)
                {
                    return (await api.UploadPhotoFileAsync(
                            id,
                            actorId,
                            new UploadHandoverPhotoFileRequest(
                                corner,
                                Stream.Null,
                                string.Empty,
                                string.Empty,
                                capturedAt),
                            ct))
                        .ToCreatedHttpResult(dto => $"/api/handovers/{id}/photos/{dto.Id}");
                }

                await using var stream = file.OpenReadStream();
                return (await api.UploadPhotoFileAsync(
                        id,
                        actorId,
                        new UploadHandoverPhotoFileRequest(
                            corner,
                            stream,
                            file.FileName,
                            file.ContentType ?? string.Empty,
                            capturedAt),
                        ct))
                    .ToCreatedHttpResult(dto => $"/api/handovers/{id}/photos/{dto.Id}");
            }

            UploadHandoverPhotoRequest? request;
            try
            {
                request = await httpContext.Request.ReadFromJsonAsync<UploadHandoverPhotoRequest>(ct);
            }
            catch (JsonException)
            {
                return Results.Problem(
                    title: "Validation Error",
                    detail: "Invalid JSON payload.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "validation.invalid_json" });
            }

            if (request is null)
            {
                return Results.Problem(
                    title: "Validation Error",
                    detail: "Request body is required.",
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?> { ["errorCode"] = "validation.null_request" });
            }

            var validationResult = await jsonValidator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                var first = validationResult.Errors[0];
                return Results.Problem(
                    title: "Validation Error",
                    detail: first.ErrorMessage,
                    statusCode: StatusCodes.Status400BadRequest,
                    extensions: new Dictionary<string, object?>
                    {
                        ["errorCode"] = string.IsNullOrWhiteSpace(first.ErrorCode) ? "validation.failed" : first.ErrorCode,
                        ["errors"] = validationResult.Errors
                            .GroupBy(e => e.PropertyName)
                            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
                    });
            }

            return (await api.AddPhotoAsync(id, actorId, request, ct))
                .ToCreatedHttpResult(dto => $"/api/handovers/{id}/photos/{dto.Id}");
        })
        .WithName("UploadHandoverPhoto")
        .WithSummary("Ghi nhận ảnh góc xe, đồng hồ Odo")
        .WithDescription("Uploads or registers an inspected vehicle photo (FrontLeft, FrontRight, RearLeft, RearRight, Dash) with SHA-256 tamper-verification hash.")
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
