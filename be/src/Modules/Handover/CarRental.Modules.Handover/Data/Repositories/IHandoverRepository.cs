using CarRental.Modules.Handover.Contracts.Enums;
using CarRental.Modules.Handover.Entities;
using HandoverEntity = CarRental.Modules.Handover.Entities.Handover;

namespace CarRental.Modules.Handover.Data.Repositories;

public interface IHandoverRepository
{
    Task<HandoverEntity?> GetByIdAsync(HandoverId id, CancellationToken ct = default);

    Task<HandoverEntity?> GetByBookingAndTypeAsync(Guid bookingId, HandoverType type, CancellationToken ct = default);

    Task<IReadOnlyList<HandoverEntity>> GetByBookingIdAsync(Guid bookingId, CancellationToken ct = default);

    Task<HandoverContext?> GetContextAsync(Guid bookingId, CancellationToken ct = default);

    void Add(HandoverEntity handover);

    void AddContext(HandoverContext context);
}
