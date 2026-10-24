using CarRental.Modules.Fleet.Contracts.Events;
using CarRental.Modules.Pricing.Contracts.Events;
using CarRental.Modules.Reviews.Contracts.Events;
using CarRental.Modules.Search.Data;
using CarRental.Modules.Search.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Search.EventHandlers;

public sealed class SearchEventHandlers
{
    public static async Task Handle(
        VehiclePublished message,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        var document = await GetOrCreateAsync(message.VehicleId, dbContext, ct);
        if (document.ApplyPublished(
                message.Brand,
                message.Model,
                message.Seats,
                message.Transmission,
                message.Location,
                message.Address,
                message.OccurredAt))
            await dbContext.SaveChangesAsync(ct);
    }

    public static async Task Handle(
        VehicleUnpublished message,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        var document = await GetOrCreateAsync(message.VehicleId, dbContext, ct);
        if (document.ApplyUnpublished(message.OccurredAt))
            await dbContext.SaveChangesAsync(ct);
    }

    public static async Task Handle(
        PricingUpdated message,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        var document = await GetOrCreateAsync(message.VehicleId, dbContext, ct);
        if (document.ApplyPrice(message.PriceFrom, message.OccurredAt))
            await dbContext.SaveChangesAsync(ct);
    }

    public static async Task Handle(
        RatingSummaryUpdated message,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        var document = await GetOrCreateAsync(message.VehicleId, dbContext, ct);
        if (document.ApplyRating(message.AverageRating, message.TotalReviews, message.OccurredAt))
            await dbContext.SaveChangesAsync(ct);
    }

    private static async Task<VehicleSearchDocument> GetOrCreateAsync(
        Guid vehicleId,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        var document = await dbContext.VehicleSearchDocuments
            .SingleOrDefaultAsync(d => d.VehicleId == vehicleId, ct);
        if (document is not null)
            return document;

        document = VehicleSearchDocument.Create(vehicleId);
        dbContext.VehicleSearchDocuments.Add(document);
        return document;
    }
}
