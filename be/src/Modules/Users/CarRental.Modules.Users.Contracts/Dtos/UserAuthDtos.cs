using System;

namespace CarRental.Modules.Users.Contracts.Dtos;

/// <summary>Credentials used to register a customer account.</summary>
public sealed record RegisterUserDto(
    string Email,
    string Phone,
    string Password);

/// <summary>Credentials used to authenticate by email or phone.</summary>
public sealed record LoginUserDto(
    string Identifier, // email or phone
    string Password);

/// <summary>Access and refresh tokens issued to an authenticated user.</summary>
public sealed record AuthResultDto(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

/// <summary>A refresh token used to obtain a new access-token pair.</summary>
public sealed record RefreshTokenDto(string RefreshToken);

/// <summary>The authenticated user identity.</summary>
public sealed record CurrentUserDto(Guid UserId, string Email, string Role);

/// <summary>Profile fields to update for a user.</summary>
public sealed record UpdateProfileDto(
    string FullName,
    string Address);
