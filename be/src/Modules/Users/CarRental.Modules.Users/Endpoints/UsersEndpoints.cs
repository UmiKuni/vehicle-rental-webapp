using System.Security.Claims;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Data.Repositories;
using CarRental.Modules.Users.Entities;
using CarRental.Modules.Users.Enums;
using CarRental.Modules.Users.ValueObjects;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Users.Endpoints;

public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users").WithTags("Users");

        group.MapPost("/register", async (RegisterUserRequest request, IUserRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var existing = await repository.GetByEmailAsync(request.Email, ct);
            if (existing != null)
            {
                return Results.Conflict(new Error("user.already_exists", "A user with this email already exists."));
            }

            PhoneNumber? phone = null;
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneResult = PhoneNumber.Create(request.PhoneNumber);
                if (phoneResult.IsFailure)
                {
                    return Results.BadRequest(phoneResult.Error);
                }
                phone = phoneResult.Value;
            }

            var userId = request.Id == Guid.Empty ? UserId.New() : new UserId(request.Id);
            var now = timeProvider.GetUtcNow();
            var registerResult = UserProfile.Register(userId, request.Email, request.FullName, phone, request.DateOfBirth, now);
            if (registerResult.IsFailure)
            {
                return Results.BadRequest(registerResult.Error);
            }

            await repository.AddAsync(registerResult.Value, ct);
            await repository.SaveChangesAsync(ct);

            return Results.Created($"/api/users/{registerResult.Value.Id}", new
            {
                UserId = registerResult.Value.Id.Value,
                registerResult.Value.Email,
                registerResult.Value.FullName
            });
        });

        group.MapGet("/me", async (ClaimsPrincipal user, IUserRepository repository, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var profile = await repository.GetByIdAsync(new UserId(userId), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            return Results.Ok(new
            {
                UserId = profile.Id.Value,
                profile.Email,
                profile.FullName,
                PhoneNumber = profile.PhoneNumber?.E164,
                profile.DateOfBirth,
                AvatarUrl = profile.Avatar?.Url,
                Roles = profile.Roles.Select(r => r.ToString()),
                Status = profile.Status.ToString(),
                HasVerifiedIdentity = profile.IdentityDocument?.Status == DocumentStatus.Verified,
                HasVerifiedLicense = profile.DriverLicense?.Status == DocumentStatus.Verified
            });
        }).RequireAuthorization();

        group.MapPut("/me/profile", async (UpdateProfileRequest request, ClaimsPrincipal user, IUserRepository repository, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var profile = await repository.GetByIdAsync(new UserId(userId), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            PhoneNumber? phone = null;
            if (!string.IsNullOrWhiteSpace(request.PhoneNumber))
            {
                var phoneResult = PhoneNumber.Create(request.PhoneNumber);
                if (phoneResult.IsFailure)
                {
                    return Results.BadRequest(phoneResult.Error);
                }
                phone = phoneResult.Value;
            }

            var result = profile.UpdateProfile(request.FullName, phone, request.DateOfBirth, request.Avatar);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.NoContent();
        }).RequireAuthorization();

        group.MapPost("/me/identity", async (SubmitIdentityRequest request, ClaimsPrincipal user, IUserRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var profile = await repository.GetByIdAsync(new UserId(userId), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = profile.SubmitIdentityDocument(request.CitizenIdNumber, request.FrontImage, request.BackImage, request.SelfieImage, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Identity document submitted for review." });
        }).RequireAuthorization();

        group.MapPost("/me/driver-license", async (SubmitDriverLicenseRequest request, ClaimsPrincipal user, IUserRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var profile = await repository.GetByIdAsync(new UserId(userId), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = profile.SubmitDriverLicense(request.Number, request.LicenseClass, request.ExpiresOn, request.Image, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Driver license submitted for review." });
        }).RequireAuthorization();

        group.MapPost("/me/become-owner", async (BecomeOwnerRequest request, ClaimsPrincipal user, IUserRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Results.Unauthorized();
            }

            var profile = await repository.GetByIdAsync(new UserId(userId), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = profile.BecomeOwner(request.PayoutAccount, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Successfully upgraded account to vehicle owner." });
        }).RequireAuthorization();

        // Admin endpoints
        var adminGroup = group.MapGroup("/admin").RequireAuthorization("Admin");

        adminGroup.MapPost("/identity/{id:guid}/approve", async (Guid id, ClaimsPrincipal admin, IUserRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var adminIdClaim = admin.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(adminIdClaim, out var adminId);

            var profile = await repository.GetByIdAsync(new UserId(id), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = profile.ApproveIdentity(adminId, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Identity approved." });
        });

        adminGroup.MapPost("/identity/{id:guid}/reject", async (Guid id, RejectRequest request, ClaimsPrincipal admin, IUserRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var adminIdClaim = admin.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(adminIdClaim, out var adminId);

            var profile = await repository.GetByIdAsync(new UserId(id), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = profile.RejectIdentity(adminId, request.Reason, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Identity rejected." });
        });

        adminGroup.MapPost("/driver-license/{id:guid}/approve", async (Guid id, ClaimsPrincipal admin, IUserRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var adminIdClaim = admin.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(adminIdClaim, out var adminId);

            var profile = await repository.GetByIdAsync(new UserId(id), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = profile.ApproveDriverLicense(adminId, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Driver license approved." });
        });

        adminGroup.MapPost("/driver-license/{id:guid}/reject", async (Guid id, RejectRequest request, ClaimsPrincipal admin, IUserRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var adminIdClaim = admin.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(adminIdClaim, out var adminId);

            var profile = await repository.GetByIdAsync(new UserId(id), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = profile.RejectDriverLicense(adminId, request.Reason, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "Driver license rejected." });
        });

        adminGroup.MapPost("/users/{id:guid}/suspend", async (Guid id, SuspendRequest request, ClaimsPrincipal admin, IUserRepository repository, TimeProvider timeProvider, CancellationToken ct) =>
        {
            var adminIdClaim = admin.FindFirstValue(ClaimTypes.NameIdentifier);
            Guid.TryParse(adminIdClaim, out var adminId);

            var profile = await repository.GetByIdAsync(new UserId(id), ct);
            if (profile == null)
            {
                return Results.NotFound();
            }

            var now = timeProvider.GetUtcNow();
            var result = profile.Suspend(request.Reason, adminId, now);
            if (result.IsFailure)
            {
                return Results.BadRequest(result.Error);
            }

            await repository.SaveChangesAsync(ct);
            return Results.Ok(new { Message = "User suspended." });
        });

        return endpoints;
    }
}

public sealed record RegisterUserRequest(Guid Id, string Email, string FullName, string? PhoneNumber, DateOnly? DateOfBirth);
public sealed record UpdateProfileRequest(string FullName, string? PhoneNumber, DateOnly? DateOfBirth, FileReference? Avatar);
public sealed record SubmitIdentityRequest(string CitizenIdNumber, FileReference FrontImage, FileReference BackImage, FileReference? SelfieImage);
public sealed record SubmitDriverLicenseRequest(string Number, LicenseClass LicenseClass, DateOnly ExpiresOn, FileReference Image);
public sealed record BecomeOwnerRequest(PayoutAccount PayoutAccount);
public sealed record RejectRequest(string Reason);
public sealed record SuspendRequest(string Reason);
