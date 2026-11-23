using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Audit.Contracts;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using CarRental.Modules.Users.Contracts.Enums;
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

        group.MapPost("/logout", async (RefreshTokenDto dto, ClaimsPrincipal principal, IUsersApi api, CancellationToken ct) =>
        {
            var (jti, expiresAt) = ExtractTokenRevocationInfo(principal);
            await api.LogoutAsync(dto.RefreshToken, jti, expiresAt, ct);
            return Results.NoContent();
        })
        .WithName("LogoutUser")
        .RequireAuthorization("UnverifiedUserAllowed")
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

        authGroup.MapPost("/logout", async (RefreshTokenDto dto, ClaimsPrincipal principal, IUsersApi api, CancellationToken ct) =>
        {
            var (jti, expiresAt) = ExtractTokenRevocationInfo(principal);
            await api.LogoutAsync(dto.RefreshToken, jti, expiresAt, ct);
            return Results.NoContent();
        })
        .WithName("AuthLogoutUser")
        .RequireAuthorization("UnverifiedUserAllowed")
        .WithSummary("Revoke refresh token (alias).")
        .Produces(StatusCodes.Status204NoContent);

        authGroup.MapGet("/me", (ClaimsPrincipal principal) =>
            Results.Ok(BuildCurrentUserDto(principal)))
        .WithName("AuthGetCurrentUser")
        .RequireAuthorization("UnverifiedUserAllowed")
        .WithSummary("Get authenticated user identity (alias).")
        .Produces<CurrentUserDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        var unverifiedAllowed = group.MapGroup(string.Empty)
            .RequireAuthorization("UnverifiedUserAllowed");

        unverifiedAllowed.MapGet("/me", (ClaimsPrincipal principal) =>
            Results.Ok(BuildCurrentUserDto(principal)))
        .WithName("GetCurrentUser")
        .WithSummary("Get the authenticated user identity.")
        .Produces<CurrentUserDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        unverifiedAllowed.MapGet("/me/verification-status", async (
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            var result = await api.GetVerificationStatusAsync(GetUserId(principal), ct);
            return result.ToHttpResult();
        })
        .WithName("GetMyVerificationStatus")
        .WithSummary("Get the current user's citizen ID and driver's license verification status.")
        .Produces<UserVerificationStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound);

        unverifiedAllowed.MapGet("/{userId:guid}/verification-status", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            CancellationToken ct) =>
        {
            if (userId != GetUserId(principal) && !principal.IsInRole("Admin"))
                return Results.Forbid();

            var result = await api.GetVerificationStatusAsync(userId, ct);
            return result.ToHttpResult();
        })
        .WithName("GetUserVerificationStatus")
        .WithSummary("Get a user's citizen ID and driver's license verification status.")
        .Produces<UserVerificationStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound);

        unverifiedAllowed.MapPost("/me/verify-id", async (
            VerifyIdDocumentDto? dto,
            ClaimsPrincipal principal,
            IUsersApi api,
            IAuditRecorder auditRecorder,
            CancellationToken ct) =>
        {
            var actorId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "VerifyIdentityDocument",
                entityName: "User",
                getEntityId: _ => actorId.ToString(),
                execute: () => api.VerifyIdAsync(actorId, dto, ct),
                captureBeforeState: async () => (await api.GetVerificationStatusAsync(actorId, ct)).Value,
                captureAfterState: res => new { res.IsIdVerified, res.IsVerified },
                actorId: actorId,
                ct: ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyMyId")
        .WithSummary("Verify the current user's citizen ID (CCCD) (mock).")
        .Produces<UserVerificationStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        unverifiedAllowed.MapPost("/{userId:guid}/verify-id", async (
            Guid userId,
            VerifyIdDocumentDto? dto,
            ClaimsPrincipal principal,
            IUsersApi api,
            IAuditRecorder auditRecorder,
            CancellationToken ct) =>
        {
            var actorId = GetUserId(principal);
            if (userId != actorId && !principal.IsInRole("Admin"))
                return Results.Forbid();

            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "VerifyIdentityDocument",
                entityName: "User",
                getEntityId: _ => userId.ToString(),
                execute: () => api.VerifyIdAsync(userId, dto, ct),
                captureBeforeState: async () => (await api.GetVerificationStatusAsync(userId, ct)).Value,
                captureAfterState: res => new { res.IsIdVerified, res.IsVerified },
                actorId: actorId,
                ct: ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyUserId")
        .WithSummary("Verify a user's citizen ID (CCCD) (mock).")
        .Produces<UserVerificationStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        unverifiedAllowed.MapPost("/me/verify-driver-license", async (
            VerifyDriverLicenseDto? dto,
            ClaimsPrincipal principal,
            IUsersApi api,
            IAuditRecorder auditRecorder,
            CancellationToken ct) =>
        {
            var actorId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "VerifyDriverLicense",
                entityName: "User",
                getEntityId: _ => actorId.ToString(),
                execute: () => api.VerifyDriverLicenseAsync(actorId, dto, ct),
                captureBeforeState: async () => (await api.GetVerificationStatusAsync(actorId, ct)).Value,
                captureAfterState: res => new { res.IsDriverLicenseVerified, res.IsVerified },
                actorId: actorId,
                ct: ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyMyDriverLicense")
        .WithSummary("Verify the current user's driver's license (GPLX) (mock).")
        .Produces<UserVerificationStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        unverifiedAllowed.MapPost("/me/documents/{type}", async (
        DocumentType type,
        IFormFile file,
        ClaimsPrincipal principal,
        IUsersApi api,
        IAuditRecorder auditRecorder,
        CancellationToken ct) =>
        {
            var actorId = GetUserId(principal);
            await using var stream = file.OpenReadStream();
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "UploadIdentityDocument",
                entityName: "User",
                getEntityId: _ => actorId.ToString(),
                execute: () => api.UploadIdentityDocumentAsync(
                    actorId,
                    type,
                    stream,
                    file.FileName,
                    file.ContentType,
                    ct),
                captureBeforeState: async () => (await api.GetVerificationStatusAsync(actorId, ct)).Value,
                captureAfterState: _ => new { DocumentType = type.ToString(), FileName = file.FileName },
                actorId: actorId,
                ct: ct);
            return result.ToHttpResult();
        })
        .DisableAntiforgery()
        .WithName("UploadMyIdentityDocument")
        .WithSummary("Upload a private citizen ID or driver's license document.")
        .Produces<UserVerificationStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized);

        unverifiedAllowed.MapPost("/{userId:guid}/verify-driver-license", async (
            Guid userId,
            VerifyDriverLicenseDto? dto,
            ClaimsPrincipal principal,
            IUsersApi api,
            IAuditRecorder auditRecorder,
            CancellationToken ct) =>
        {
            var actorId = GetUserId(principal);
            if (userId != actorId && !principal.IsInRole("Admin"))
                return Results.Forbid();

            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "VerifyDriverLicense",
                entityName: "User",
                getEntityId: _ => userId.ToString(),
                execute: () => api.VerifyDriverLicenseAsync(userId, dto, ct),
                captureBeforeState: async () => (await api.GetVerificationStatusAsync(userId, ct)).Value,
                captureAfterState: res => new { res.IsDriverLicenseVerified, res.IsVerified },
                actorId: actorId,
                ct: ct);
            return result.ToHttpResult();
        })
        .WithName("VerifyUserDriverLicense")
        .WithSummary("Verify a user's driver's license (GPLX) (mock).")
        .Produces<UserVerificationStatusDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict);

        var authenticated = group.MapGroup(string.Empty)
            .RequireAuthorization("AuthenticatedUser");

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

        admin.MapGet("/", async (
            IUsersApi api,
            int page = 1,
            int pageSize = 10,
            string? search = null,
            string? role = null,
            string? status = null,
            CancellationToken ct = default) =>
        {
            var result = await api.GetAllUsersAsync(page, pageSize, search, role, status, ct);
            return result.ToHttpResult();
        })
        .WithName("AdminGetAllUsers")
        .WithSummary("Get all users (paginated, Admin only) (mock).")
        .Produces<PagedResult<AdminUserDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        admin.MapPut("/{userId:guid}/ban", async (
            Guid userId,
            ClaimsPrincipal principal,
            IUsersApi api,
            IAuditRecorder auditRecorder,
            CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "BanUser",
                entityName: "User",
                entityId: userId.ToString(),
                execute: () => api.BanAsync(userId, adminId, ct),
                captureBeforeState: async () => (await api.GetProfileAsync(userId, ct)).Value,
                captureAfterState: () => new { Status = "Banned", BannedBy = adminId },
                actorId: adminId,
                ct: ct);
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
            IAuditRecorder auditRecorder,
            CancellationToken ct) =>
        {
            var adminId = GetUserId(principal);
            var result = await auditRecorder.ExecuteWithAuditAsync(
                action: "UnbanUser",
                entityName: "User",
                entityId: userId.ToString(),
                execute: () => api.UnbanAsync(userId, adminId, ct),
                captureBeforeState: async () => (await api.GetProfileAsync(userId, ct)).Value,
                captureAfterState: () => new { Status = "Active", UnbannedBy = adminId },
                actorId: adminId,
                ct: ct);
            return result.ToHttpResult();
        })
        .WithName("UnbanUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound);
    }

    private static CurrentUserDto BuildCurrentUserDto(ClaimsPrincipal principal)
    {
        var userId = GetUserId(principal);
        var isIdVerified = string.Equals(principal.FindFirstValue("id_verified"), "true", StringComparison.OrdinalIgnoreCase);
        var isDriverLicenseVerified = string.Equals(principal.FindFirstValue("driver_license_verified"), "true", StringComparison.OrdinalIgnoreCase);
        var isVerified = string.Equals(principal.FindFirstValue("verified_user"), "true", StringComparison.OrdinalIgnoreCase);

        return new CurrentUserDto(
            userId,
            principal.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
            principal.FindFirstValue(ClaimTypes.Role) ?? string.Empty,
            isIdVerified,
            isDriverLicenseVerified,
            isVerified);
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
    }

    private static (string? Jti, DateTimeOffset? ExpiresAt) ExtractTokenRevocationInfo(ClaimsPrincipal principal)
    {
        var jti = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);
        DateTimeOffset? expiresAt = null;
        var expClaim = principal.FindFirstValue(JwtRegisteredClaimNames.Exp);
        if (long.TryParse(expClaim, NumberStyles.Integer, CultureInfo.InvariantCulture, out var expSeconds))
            expiresAt = DateTimeOffset.FromUnixTimeSeconds(expSeconds);

        return (jti, expiresAt);
    }
}
