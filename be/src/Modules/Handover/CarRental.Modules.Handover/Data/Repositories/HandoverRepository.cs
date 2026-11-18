using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Entities;
using Microsoft.EntityFrameworkCore;
using HandoverEntity = CarRental.Modules.Handover.Entities.Handover;

namespace CarRental.Modules.Handover.Data.Repositories;

internal sealed class HandoverRepository : IHandoverRepository
{
    private readonly HandoverDbContext _dbContext;

    public HandoverRepository(HandoverDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<HandoverEntity?> GetByIdAsync(HandoverId id, CancellationToken ct = default) =>
        _dbContext.Handovers
            .Include(h => h.Photos)
            .Include(h => h.Condition)
            .SingleOrDefaultAsync(h => h.Id == id, ct);

    public Task<HandoverEntity?> GetByBookingAndTypeAsync(Guid bookingId, HandoverType type, CancellationToken ct = default) =>
        _dbContext.Handovers
            .Include(h => h.Photos)
            .Include(h => h.Condition)
            .SingleOrDefaultAsync(h => h.BookingId == bookingId && h.Type == type, ct);

    public async Task<IReadOnlyList<HandoverEntity>> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default) =>
        await _dbContext.Handovers
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(h => h.BookingId == bookingId)
            .OrderBy(h => h.ServerReceivedAt)
            .ToListAsync(ct);

    public Task<HandoverContext?> GetContextAsync(Guid bookingId, CancellationToken ct = default) =>
        _dbContext.HandoverContexts
            .SingleOrDefaultAsync(c => c.BookingId == bookingId, ct);

    public void Add(HandoverEntity handover) =>
        _dbContext.Handovers.Add(handover);

    public void AddContext(HandoverContext context) =>
        _dbContext.HandoverContexts.Add(context);
}
