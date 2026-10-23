using System;
using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Users.Contracts.Dtos;

public sealed record RegisterUserDto(
    string Email,
    string Phone,
    string Password);

public sealed record LoginUserDto(
    string Identifier, // email or phone
    string Password);

public sealed record AuthResultDto(
    Guid UserId,
    string Token);

public sealed record UpdateProfileDto(
    string FullName,
    string Address);
