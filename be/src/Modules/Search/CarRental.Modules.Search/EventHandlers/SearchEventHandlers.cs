using CarRental.Modules.Fleet.Contracts.Events;
using CarRental.Modules.Pricing.Contracts.Events;
using CarRental.Modules.Reviews.Contracts.Events;
using CarRental.Modules.Search.Data;
using CarRental.Modules.Search.Entities;
using Microsoft.EntityFrameworkCore;
using Wolverine.Attributes;

namespace CarRental.Modules.Search.EventHandlers;

[WolverineHandler]
public sealed class SearchEventHandlers
{
    private static readonly SemaphoreSlim ProjectionLock = new(1, 1);

    public async Task Handle(
        VehiclePublished message,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        await ProjectionLock.WaitAsync(ct);
        try
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
        finally
        {
            ProjectionLock.Release();
        }
    }

    public async Task Handle(
        VehicleUnpublished message,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        await ProjectionLock.WaitAsync(ct);
        try
        {
            var document = await GetOrCreateAsync(message.VehicleId, dbContext, ct);
            if (document.ApplyUnpublished(message.OccurredAt))
                await dbContext.SaveChangesAsync(ct);
        }
        finally
        {
            ProjectionLock.Release();
        }
    }

    public async Task Handle(
        PricingUpdated message,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        await ProjectionLock.WaitAsync(ct);
        try
        {
            var document = await GetOrCreateAsync(message.VehicleId, dbContext, ct);
            if (document.ApplyPrice(message.PriceFrom, message.OccurredAt))
                await dbContext.SaveChangesAsync(ct);
        }
        finally
        {
            ProjectionLock.Release();
        }
    }

    public async Task Handle(
        RatingSummaryUpdated message,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        await ProjectionLock.WaitAsync(ct);
        try
        {
            var document = await GetOrCreateAsync(message.VehicleId, dbContext, ct);
            if (document.ApplyRating(message.AverageRating, message.TotalReviews, message.OccurredAt))
                await dbContext.SaveChangesAsync(ct);
        }
        finally
        {
            ProjectionLock.Release();
        }
    }

    private static async Task<VehicleSearchDocument> GetOrCreateAsync(
        Guid vehicleId,
        SearchDbContext dbContext,
        CancellationToken ct)
    {
        var document = await dbContext.VehicleSearchDocuments
            .SingleOrDefaultAsync(d => d.Id == vehicleId, ct);
        if (document is not null)
            return document;

        document = VehicleSearchDocument.Create(vehicleId);
        dbContext.VehicleSearchDocuments.Add(document);
        return document;
    }
}
