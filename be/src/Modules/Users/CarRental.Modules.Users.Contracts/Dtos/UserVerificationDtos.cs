using System;
using System.Collections.Generic;
using CarRental.Modules.Users.Contracts.Enums;

namespace CarRental.Modules.Users.Contracts.Dtos;

/// <summary>Request payload for mock citizen ID (CCCD) verification.</summary>
public sealed record VerifyIdDocumentDto(
    string? DocumentNumber = null,
    string? FullName = null,
    string? Address = null,
    string? StorageKey = null);

/// <summary>Request payload for mock driver's license (GPLX) verification.</summary>
public sealed record VerifyDriverLicenseDto(
    string? LicenseNumber = null,
    LicenseClass? LicenseClass = null,
    string? StorageKey = null);

/// <summary>Summary of a submitted user identity document.</summary>
public sealed record IdentityDocumentDto(
    Guid DocumentId,
    DocumentType Type,
    string StorageKey,
    DocumentVerificationStatus VerificationStatus,
    string? RejectReason);

/// <summary>Verification status across citizen ID and driver's license for a user.</summary>
public sealed record UserVerificationStatusDto(
    Guid UserId,
    bool IsIdVerified,
    bool IsDriverLicenseVerified,
    bool IsVerified,
    IReadOnlyList<IdentityDocumentDto> Documents);

/// <summary>Administrative summary view of a user account.</summary>
public sealed record AdminUserDto(
    Guid UserId,
    string Email,
    string Phone,
    string FullName,
    string Address,
    string Role,
    string Status,
    bool IsIdVerified,
    bool IsDriverLicenseVerified,
    bool IsVerified,
    IReadOnlyList<IdentityDocumentDto> IdentityDocuments);

