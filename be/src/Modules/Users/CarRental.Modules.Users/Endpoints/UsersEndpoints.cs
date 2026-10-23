// Auto‑generated: Minimal‑API endpoints for the Users module
using System;
using System.Threading.Tasks;
using CarRental.Modules.Users.Contracts;
using CarRental.Modules.Users.Contracts.Dtos;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Http;

namespace CarRental.Modules.Users.Endpoints;

public static class UsersEndpoints
{
    public static void MapUsersEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users");

        // POST register user
        group.MapPost("/register", async (RegisterUserDto dto, IUsersApi api) =>
        {
            var result = await api.RegisterAsync(dto);
            return result.IsSuccess
                ? Results.Created($"/api/users/{result.Value.UserId}", result.Value)
                : Results.BadRequest(result.Error.Message);
        })
        .WithName("RegisterUser");

        // POST login user
        group.MapPost("/login", async (LoginUserDto dto, IUsersApi api) =>
        {
            var result = await api.LoginAsync(dto);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .WithName("LoginUser");

        // PUT user profile
        group.MapPut("/{userId:guid}/profile", async (Guid userId, UpdateProfileDto dto, IUsersApi api) =>
        {
            var result = await api.UpdateProfileAsync(userId, dto);
            return result.IsSuccess
                ? Results.NoContent()
                : Results.NotFound(result.Error.Message);
        })
        .WithName("UpdateUserProfile");

        // GET full user profile
        group.MapGet("/{userId:guid}/profile", async (Guid userId, IUsersApi api) =>
        {
            var result = await api.GetProfileAsync(userId);
            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(result.Error.Message);
        })
        .WithName("GetUserProfile");

        // GET booking eligibility
        group.MapGet("/eligibility/{userId:guid}", async (Guid userId, IUsersApi api) =>
        {
            var result = await api.GetBookingEligibilityAsync(userId);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.Problem(result.Error.Message);
        })
        .WithName("GetBookingEligibility");

        // GET user contact info
        group.MapGet("/contact/{userId:guid}", async (Guid userId, IUsersApi api) =>
        {
            var result = await api.GetContactAsync(userId);
            return result.IsSuccess ? Results.Ok(result.Value) : Results.NotFound(result.Error.Message);
        })
        .WithName("GetUserContact");

        // PUT ban user
        group.MapPut("/ban/{userId:guid}/{adminId:guid}", async (Guid userId, Guid adminId, IUsersApi api) =>
        {
            var result = await api.BanAsync(userId, adminId);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Error.Message);
        })
        .WithName("BanUser");

        // PUT unban user
        group.MapPut("/unban/{userId:guid}/{adminId:guid}", async (Guid userId, Guid adminId, IUsersApi api) =>
        {
            var result = await api.UnbanAsync(userId, adminId);
            return result.IsSuccess ? Results.Ok() : Results.Problem(result.Error.Message);
        })
        .WithName("UnbanUser");
    }
}
