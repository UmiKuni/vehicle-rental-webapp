using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
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
            return result.ToCreatedHttpResult(u => $"/api/users/{u.UserId}");
        })
        .WithName("RegisterUser")
        .WithSummary("Register a customer account.")
        .Produces<AuthResultDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (LoginUserDto dto, IUsersApi api, CancellationToken ct) =>
        {
            var result = await api.LoginAsync(dto, ct);
            return result.ToHttpResult();
        })
        .WithName("LoginUser")
        .WithSummary("Authenticate with an email or phone number.")
        .Produces<AuthResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/refresh", async (RefreshTokenDto dto, IUsersApi api, CancellationToken ct) =>
        {
            var result = await api.RefreshAsync(dto.RefreshToken, null, ct);
            return result.ToHttpResult();
        })
        .WithName("RefreshUserToken")
        .WithSummary("Rotate a refresh token and issue a new access token.")
        .Produces<AuthResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/logout", async (RefreshTokenDto dto, IUsersApi api, CancellationToken ct) =>
        {
            await api.LogoutAsync(dto.RefreshToken, ct);
            return Results.NoContent();
        })
        .WithName("LogoutUser")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Revoke a refresh token.")
        .Produces(StatusCodes.Status204NoContent);

        // Route aliases for /api/auth/* to support both conventions seamlessly
        var authGroup = routes.MapGroup("/api/auth").WithTags("Authentication");

        authGroup.MapPost("/register", async (RegisterUserDto dto, IUsersApi api, CancellationToken ct) =>
        {
            var result = await api.RegisterAsync(dto, ct);
            return result.ToCreatedHttpResult(u => $"/api/users/{u.UserId}");
        })
        .WithName("AuthRegisterUser")
        .WithSummary("Register a customer account (alias).")
        .Produces<AuthResultDto>(StatusCodes.Status201Created)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status409Conflict);

        authGroup.MapPost("/login", async (LoginUserDto dto, IUsersApi api, CancellationToken ct) =>
        {
            var result = await api.LoginAsync(dto, ct);
            return result.ToHttpResult();
        })
        .WithName("AuthLoginUser")
        .WithSummary("Authenticate with email/phone (alias).")
        .Produces<AuthResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        authGroup.MapPost("/refresh", async (RefreshTokenDto dto, IUsersApi api, CancellationToken ct) =>
        {
            var result = await api.RefreshAsync(dto.RefreshToken, null, ct);
            return result.ToHttpResult();
        })
        .WithName("AuthRefreshUserToken")
        .WithSummary("Rotate refresh token (alias).")
        .Produces<AuthResultDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        authGroup.MapPost("/logout", async (RefreshTokenDto dto, IUsersApi api, CancellationToken ct) =>
        {
            await api.LogoutAsync(dto.RefreshToken, ct);
            return Results.NoContent();
        })
        .WithName("AuthLogoutUser")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Revoke refresh token (alias).")
        .Produces(StatusCodes.Status204NoContent);

        authGroup.MapGet("/me", (ClaimsPrincipal principal) =>
        {
            var userId = GetUserId(principal);
            return Results.Ok(new CurrentUserDto(
                userId,
                principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
                principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty));
        })
        .WithName("AuthGetCurrentUser")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Get authenticated user identity (alias).")
        .Produces<CurrentUserDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

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
        .WithSummary("Get the authenticated user identity.")
        .Produces<CurrentUserDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

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
            return result.ToHttpResult();
        })
        .WithName("UpdateUserProfile")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        authenticated.MapGet("/{userId:guid}/profile", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            if (userId != GetUserId(principal))
                return Results.Forbid();

            var result = await api.GetProfileAsync(userId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetUserProfile")
        .Produces<UserProfileDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        authenticated.MapGet("/eligibility/{userId:guid}", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            if (userId != GetUserId(principal))
                return Results.Forbid();

            var result = await api.GetBookingEligibilityAsync(userId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetBookingEligibility")
        .Produces<BookingEligibilityDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        authenticated.MapGet("/contact/{userId:guid}", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            if (userId != GetUserId(principal))
                return Results.Forbid();

            var result = await api.GetContactAsync(userId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetUserContact")
        .Produces<UserContactDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound);

        var admin = group.MapGroup(string.Empty)
            .RequireAuthorization("AdminOnly");

        admin.MapPut("/{userId:guid}/ban", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            var result = await api.BanAsync(userId, GetUserId(principal), ct);
            return result.ToHttpResult();
        })
        .WithName("BanUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);

        admin.MapPut("/{userId:guid}/unban", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            var result = await api.UnbanAsync(userId, GetUserId(principal), ct);
            return result.ToHttpResult();
        })
        .WithName("UnbanUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
    }
}
