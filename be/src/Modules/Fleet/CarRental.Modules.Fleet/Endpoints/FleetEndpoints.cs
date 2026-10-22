using System.Security.Claims;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Fleet.Data.Repositories;
using CarRental.Modules.Fleet.Entities;
using CarRental.Modules.Fleet.Enums;
using CarRental.Modules.Fleet.ValueObjects;
using CarRental.Modules.Users.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Fleet.Endpoints;

public static class FleetEndpoints
{
    public static IEndpointRouteBuilder MapFleetEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/vehicles").WithTags("Fleet");

        group.MapPost("/", async (CreateVehicleRequest request, ClaimsPrincipal user, IUsersApi usersApi, IVehicleRepository repository, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var ownerId))
            {
                return Results.Unauthorized();
            }

            var isVerifiedOwner = await usersApi.IsVerifiedOwnerAsync(ownerId, ct);
            if (!isVerifiedOwner)
            {
                return Results.BadRequest(new Error("fleet.not_verified_owner", "Only verified vehicle owners can list vehicles."));
            }

            var plateResult = LicensePlate.Create(request.LicensePlate);
            if (plateResult.IsFailure)
            {
                return Results.BadRequest(plateResult.Error);
            }

            var existingPlate = await repository.GetByLicensePlateAsync(plateResult.Value, ct);
            if (existingPlate != null)
            {
                return Results.Conflict(new Error("fleet.plate_exists", "A vehicle with this license plate already exists."));
            }

            var vehicleId = request.Id == Guid.Empty ? VehicleId.New() : new VehicleId(request.Id);
            var result = Vehicle.Create(
                vehicleId,
                ownerId,
                request.Brand,
                request.Model,
                request.Year,
                plateResult.Value,
                request.Seats,
                request.Transmission,
                request.FuelType,
                request.Title,
                request.Description,
                request.Address,
                request.Rules,
                request.Features,
                isOwnerVerified: true);

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.AddAsync(result.Value, ct);
            await repository.SaveChangesAsync(ct);

            return Results.Created($"/api/vehicles/{result.Value.Id}", new
            {
                VehicleId = result.Value.Id.Value,
                result.Value.Title,
                Status = result.Value.Status.ToString()
            });
        }).RequireAuthorization();

        group.MapGet("/{id:guid}", async (Guid id, IVehicleRepository repository, CancellationToken ct) =>
        {
            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                VehicleId = vehicle.Id.Value,
                vehicle.OwnerId,
                vehicle.Brand,
                vehicle.Model,
                vehicle.Year,
                LicensePlate = vehicle.LicensePlate.Value,
                vehicle.Seats,
                Transmission = vehicle.Transmission.ToString(),
                FuelType = vehicle.FuelType.ToString(),
                vehicle.Title,
                vehicle.Description,
                vehicle.Address,
                Rules = vehicle.Rules,
                Features = vehicle.Features.Select(f => f.ToString()),
                Status = vehicle.Status.ToString(),
                Photos = vehicle.Photos.Select(p => new
                {
                    PhotoId = p.Id,
                    p.Photo.Url,
                    p.SortOrder,
                    p.IsPrimary
                }),
                Documents = vehicle.Documents.Select(d => new
                {
                    DocumentId = d.Id,
                    Type = d.Type.ToString(),
                    Status = d.Status.ToString(),
                    d.ExpiresOn
                })
            });
        });

        group.MapPut("/{id:guid}", async (Guid id, UpdateVehicleRequest request, ClaimsPrincipal user, IVehicleRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            if (vehicle.OwnerId != userId)
            {
                return Results.Forbid();
            }

            var now = timeProvider.GetUtcNow();
            var result = vehicle.UpdateDetails(
                request.Title,
                request.Description,
                request.Brand,
                request.Model,
                request.Year,
                request.Seats,
                request.Transmission,
                request.FuelType,
                request.Rules,
                request.Features,
                now);

            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/photos", async (Guid id, AddPhotoRequest request, ClaimsPrincipal user, IVehicleRepository repository, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            if (vehicle.OwnerId != userId)
            {
                return Results.Forbid();
            }

            var result = vehicle.AddPhoto(request.Photo, request.IsPrimary);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Photo added successfully." });
        }).RequireAuthorization();

        group.MapDelete("/{id:guid}/photos/{photoId:guid}", async (Guid id, Guid photoId, ClaimsPrincipal user, IVehicleRepository repository, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            if (vehicle.OwnerId != userId)
            {
                return Results.Forbid();
            }

            var result = vehicle.RemovePhoto(photoId);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/photos/{photoId:guid}/primary", async (Guid id, Guid photoId, ClaimsPrincipal user, IVehicleRepository repository, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            if (vehicle.OwnerId != userId)
            {
                return Results.Forbid();
            }

            var result = vehicle.SetPrimaryPhoto(photoId);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Primary photo updated." });
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/documents", async (Guid id, AddDocumentRequest request, ClaimsPrincipal user, IVehicleRepository repository, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            if (vehicle.OwnerId != userId)
            {
                return Results.Forbid();
            }

            var result = vehicle.AddDocument(request.Type, request.File, request.ExpiresOn);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Document uploaded successfully." });
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/submit", async (Guid id, ClaimsPrincipal user, IVehicleRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            if (vehicle.OwnerId != userId)
            {
                return Results.Forbid();
            }

            var now = timeProvider.GetUtcNow();
            var result = vehicle.Submit(now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Vehicle submitted for approval." });
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/pause", async (Guid id, ClaimsPrincipal user, IVehicleRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            if (vehicle.OwnerId != userId)
            {
                return Results.Forbid();
            }

            var now = timeProvider.GetUtcNow();
            var result = vehicle.Pause(now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Vehicle paused." });
        }).RequireAuthorization();

        group.MapPost("/{id:guid}/resume", async (Guid id, ClaimsPrincipal user, IVehicleRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            if (vehicle.OwnerId != userId)
            {
                return Results.Forbid();
            }

            var now = timeProvider.GetUtcNow();
            var result = vehicle.Resume(now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Vehicle resumed." });
        }).RequireAuthorization();

        // Admin endpoints
        var adminGroup = group.MapGroup("/admin").RequireAuthorization("Admin");

        adminGroup.MapPost("/{id:guid}/approve", async (Guid id, ClaimsPrincipal admin, IVehicleRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var adminIdClaim = admin.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(adminIdClaim, out var adminId);

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = vehicle.Approve(adminId, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Vehicle approved." });
        });

        adminGroup.MapPost("/{id:guid}/reject", async (Guid id, RejectVehicleRequest request, ClaimsPrincipal admin, IVehicleRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var adminIdClaim = admin.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(adminIdClaim, out var adminId);

            var vehicle = await repository.GetByIdAsync(new VehicleId(id), ct);
            if (vehicle == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = vehicle.Reject(adminId, request.Reason, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Vehicle rejected." });
        });

        return endpoints;
    }
}

public sealed record CreateVehicleRequest(
    Guid Id,
    string Brand,
    string Model,
    int Year,
    string LicensePlate,
    int Seats,
    TransmissionType Transmission,
    FuelType FuelType,
    string Title,
    string Description,
    Address Address,
    VehicleRules Rules,
    List<VehicleFeature>? Features);

public sealed record UpdateVehicleRequest(
    string Title,
    string Description,
    string Brand,
    string Model,
    int Year,
    int Seats,
    TransmissionType Transmission,
    FuelType FuelType,
    VehicleRules Rules,
    List<VehicleFeature>? Features);

public sealed record AddPhotoRequest(FileReference Photo, bool IsPrimary);
public sealed record AddDocumentRequest(VehicleDocumentType Type, FileReference File, DateOnly? ExpiresOn);
public sealed record RejectVehicleRequest(string Reason);
