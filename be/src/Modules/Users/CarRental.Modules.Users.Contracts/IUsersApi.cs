using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Users.Contracts.Dtos;

namespace CarRental.Modules.Users.Contracts;

public interface IUsersApi
{
    Task<Result<BookingEligibilityDto>> GetBookingEligibilityAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserContactDto>> GetContactAsync(Guid userId, CancellationToken ct = default);
}
