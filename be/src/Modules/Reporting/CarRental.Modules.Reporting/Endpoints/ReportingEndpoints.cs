using System.Security.Claims;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Reporting.Contracts;
using CarRental.Modules.Reporting.Contracts.Dtos;
using CarRental.Modules.Reporting.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CarRental.Modules.Reporting.Endpoints;

public static class ReportingEndpoints
{
    public static IEndpointRouteBuilder MapReportingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/reporting")
            .WithTags("Reporting");

        // 1. Get real-time high-level admin dashboard statistics
        group.MapGet("/dashboard", async (
            IReportingQueries queries,
            CancellationToken ct) =>
            (await queries.GetDashboardStatsAsync(ct)).ToHttpResult())
        .WithName("GetAdminDashboardStats")
        .RequireAuthorization("AdminOnly")
        .WithSummary("Số liệu tổng quan thời gian thực cho Admin")
        .WithDescription("Provides high-level platform analytics including active users, vehicles, ongoing rentals, monthly gross revenue, and cancellation rates.")
        .Produces<AdminDashboardStatsDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // 2. Get daily booking and revenue time-series
        group.MapGet("/daily-stats", async (
            DateOnly? from,
            DateOnly? to,
            IReportingQueries queries,
            CancellationToken ct) =>
            (await queries.GetDailyStatsAsync(from, to, ct)).ToHttpResult())
        .WithName("GetDailyBookingStats")
        .RequireAuthorization("AdminOnly")
        .WithSummary("Biểu đồ doanh thu & số cuốc theo ngày")
        .WithDescription("Retrieves daily aggregated booking count, revenue, and cancellation metrics for trend charts.")
        .Produces<IReadOnlyList<DailyBookingStatsDto>>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        // 3. Get owner monthly revenue and trip breakdown
        group.MapGet("/owner/revenue", async (
            Guid? ownerId,
            int? month,
            int? year,
            ClaimsPrincipal principal,
            IReportingQueries queries,
            CancellationToken ct) =>
        {
            var targetOwnerResult = ResolveTargetOwnerId(principal, ownerId);
            if (targetOwnerResult.IsFailure)
                return targetOwnerResult.Error.ToProblemResult();

            return (await queries.GetOwnerRevenueAsync(targetOwnerResult.Value, month, year, ct)).ToHttpResult();
        })
        .WithName("GetOwnerRevenueReport")
        .RequireAuthorization("AuthenticatedUser")
        .WithSummary("Báo cáo doanh thu & chuyến đi cho Chủ xe")
        .WithDescription("Generates a detailed monthly revenue report for a specific vehicle owner, detailing gross earnings, platform commission (10%), and net payout.")
        .Produces<OwnerRevenueReportDto>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status401Unauthorized)
        .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static Result<Guid> ResolveTargetOwnerId(ClaimsPrincipal principal, Guid? ownerId)
    {
        if (principal.IsInRole("Admin"))
            return ownerId ?? Guid.Empty;

        if (!principal.IsInRole("Owner"))
            return ReportingErrors.ForbiddenOwnerAccess;

        var callerId = GetUserId(principal);
        if (ownerId.HasValue && ownerId.Value != Guid.Empty && ownerId.Value != callerId)
            return ReportingErrors.ForbiddenOwnerAccess;

        return callerId;
    }

    private static Guid GetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(claim, out var userId)
            ? userId
            : throw new UnauthorizedAccessException("The authenticated user identifier is invalid.");
    }
}
