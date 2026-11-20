namespace CarRental.Modules.Handover.Contracts.Dtos;

public sealed record CreateHandoverRequest(
    Guid BookingId,
    string Type);

public sealed record UpdateVehicleConditionRequest(
    int Odo,
    int FuelLevel,
    string? DamageNote);

public sealed record VehicleConditionDto(
    Guid HandoverId,
    int Odo,
    int FuelLevel,
    string? DamageNote);

public sealed record UploadHandoverPhotoRequest(
    string Corner,
    string StorageKey,
    string FileHash,
    DateTimeOffset ClientCapturedAt);

public sealed record UploadHandoverPhotoFileRequest(
    string Corner,
    Stream Content,
    string FileName,
    string ContentType,
    DateTimeOffset? ClientCapturedAt = null);

public sealed record HandoverPhotoDto(
    Guid Id,
    Guid HandoverId,
    string Corner,
    string StorageKey,
    string FileHash,
    string ViewUrl,
    DateTimeOffset ClientCapturedAt,
    DateTimeOffset CreatedAt);

public sealed record ConfirmHandoverRequest(
    string ConfirmedByRole,
    string? SignatureToken);

public sealed record HandoverDetailDto(
    Guid Id,
    Guid BookingId,
    string Type,
    string Status,
    DateTimeOffset ServerReceivedAt,
    VehicleConditionDto? Condition,
    IReadOnlyList<HandoverPhotoDto> Photos,
    bool IsConfirmedByOwner,
    bool IsConfirmedByCustomer,
    DateTimeOffset? FinalizedAt);

public sealed record HandoverSummaryDto(
    Guid Id,
    Guid BookingId,
    string Type,
    string Status,
    DateTimeOffset ServerReceivedAt);
