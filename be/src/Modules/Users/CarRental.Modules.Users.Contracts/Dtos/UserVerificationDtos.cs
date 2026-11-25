using System;
using System.Collections.Generic;
using CarRental.Modules.Users.Contracts.Enums;

namespace CarRental.Modules.Users.Contracts.Dtos;

/// <summary>Submit citizen ID (CCCD) details and its uploaded document for administrator review.</summary>
public sealed record VerifyIdDocumentDto(
    string? DocumentNumber = null,
    string? FullName = null,
    string? Address = null,
    string? StorageKey = null);

/// <summary>Submit driver's license (GPLX) details and its uploaded document for administrator review.</summary>
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
    string? RejectReason,
    string? AccessUrl = null);

/// <summary>Verification status across citizen ID and driver's license for a user.</summary>
public sealed record UserVerificationStatusDto(
    Guid UserId,
    bool IsIdVerified,
    bool IsDriverLicenseVerified,
    bool IsVerified,
    IReadOnlyList<IdentityDocumentDto> Documents);

/// <summary>Summary of a pending document verification for admin review.</summary>
public sealed record PendingVerificationDto(
    Guid UserId,
    string Email,
    string Phone,
    string FullName,
    Guid DocumentId,
    DocumentType DocumentType,
    string StorageKey,
    DocumentVerificationStatus Status,
    string? AccessUrl = null);

/// <summary>Administrative review decision for an identity document.</summary>
public sealed record ReviewIdentityDocumentDto(
    bool Approved,
    string? RejectReason = null);

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

