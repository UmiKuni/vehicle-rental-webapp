using System.Security.Claims;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Users.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users").WithTags("Users");

        group.MapPost("/register", async (RegisterUserDto dto, IUsersApi api, CancellationToken ct) =>
        {
            var result = await api.RegisterAsync(dto, ct);
            return result.IsSuccess
                ? Results.Created($"/api/users/{result.Value.UserId}", result.Value)
                : Results.BadRequest(result.Error.Message);
        })
        .WithName("RegisterUser")
        .WithSummary("Register a customer account.");

        group.MapPost("/login", async (LoginUserDto dto, IUsersApi api, CancellationToken ct) =>
        {
            var result = await api.LoginAsync(dto, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .WithName("LoginUser")
        .WithSummary("Authenticate with an email or phone number.");

        group.MapPost("/refresh", async (RefreshTokenDto dto, IUsersApi api, CancellationToken ct) =>
        {
            var result = await api.RefreshAsync(dto.RefreshToken, null, ct);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .WithName("RefreshUserToken")
        .WithSummary("Rotate a refresh token and issue a new access token.");

        group.MapPost("/logout", async (RefreshTokenDto dto, IUsersApi api, CancellationToken ct) =>
        {
            await api.LogoutAsync(dto.RefreshToken, ct);
            return Results.NoContent();
        })
        .WithName("LogoutUser")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Revoke a refresh token.");

        var authenticated = group.MapGroup(string.Empty)
            .RequireAuthorization("AuthenticatedUser");

        authenticated.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var userId = GetUserId(principal);
            return Results.Ok(new CurrentUserDto(
                userId,
                principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty));
        })
        .WithName("GetCurrentUser")
        .WithSummary("Get the authenticated user identity.");

        authenticated.MapPut("/{userId:guid}/profile", async (
            Guid userId,
            UpdateProfileDto dto,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            if (userId != GetUserId(principal))
                return Results.Forbid();

            var result = await api.UpdateProfileAsync(userId, dto, ct);
            return result.IsSuccess ? Results.NoContent() : Results.NotFound(result.Error.Message);
        })
        .WithName("UpdateUserProfile");

        authenticated.MapGet("/{userId:guid}/profile", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            if (userId != GetUserId(principal))
                return Results.Forbid();

            var result = await api.GetProfileAsync(userId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Message);
        })
        .WithName("GetUserProfile");

        authenticated.MapGet("/eligibility/{userId:guid}", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            if (userId != GetUserId(principal))
                return Results.Forbid();

            var result = await api.GetBookingEligibilityAsync(userId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Message);
        })
        .WithName("GetBookingEligibility");

        authenticated.MapGet("/contact/{userId:guid}", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            if (userId != GetUserId(principal))
                return Results.Forbid();

            var result = await api.GetContactAsync(userId, ct);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Message);
        })
        .WithName("GetUserContact");

        var admin = group.MapGroup(string.Empty)
            .RequireAuthorization("AdminOnly");

        admin.MapPut("/{userId:guid}/ban", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            var result = await api.BanAsync(userId, GetUserId(principal), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Message);
        })
        .WithName("BanUser");

        admin.MapPut("/{userId:guid}/unban", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            var result = await api.UnbanAsync(userId, GetUserId(principal), ct);
            return result.IsSuccess ? Results.NoContent() : Results.Problem(result.Error.Message);
        })
        .WithName("UnbanUser");
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
    }
}
