using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Handover.Contracts.Dtos;

namespace CarRental.Modules.Handover.Contracts;

/// <summary>
/// Synchronous in-process contract for vehicle pickup and return handover workflows.
/// </summary>
public interface IHandoverApi
{
    Task<Result<HandoverDetailDto>> CreateAsync(
        Guid actorId,
        CreateHandoverRequest request,
        CancellationToken ct = default);

    Task<Result<IReadOnlyList<HandoverSummaryDto>>> GetByBookingAsync(
        Guid bookingId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default);

    Task<Result<HandoverDetailDto>> GetByIdAsync(
        Guid handoverId,
        Guid actorId,
        bool isAdmin = false,
        CancellationToken ct = default);

    Task<Result<VehicleConditionDto>> UpdateConditionAsync(
        Guid handoverId,
        Guid actorId,
        UpdateVehicleConditionRequest request,
        CancellationToken ct = default);

    Task<Result<HandoverPhotoDto>> AddPhotoAsync(
        Guid handoverId,
        Guid actorId,
        UploadHandoverPhotoRequest request,
        CancellationToken ct = default);

    Task<Result<HandoverPhotoDto>> UploadPhotoFileAsync(
        Guid handoverId,
        Guid actorId,
        UploadHandoverPhotoFileRequest request,
        CancellationToken ct = default);

    Task<Result<HandoverDetailDto>> ConfirmAsync(
        Guid handoverId,
        Guid actorId,
        ConfirmHandoverRequest request,
        CancellationToken ct = default);
}
