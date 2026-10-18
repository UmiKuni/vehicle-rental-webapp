# Users Module — Implementation Plan

> **Module Directory:** `src/Modules/Users`  
> **Projects:** `CarRental.Modules.Users.csproj`, `CarRental.Modules.Users.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-users`  
> **Phase:** 1 (Catalog & Core Foundation — Parallel Stream 1)  
> **PostgreSQL Schema:** `users`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.6 & 5)

---

## 1. Objectives & Scope
The Users module handles user profiles, driver licenses (GPLX), citizen identity cards (CCCD) verification, owner onboarding, and renter eligibility checks. It works alongside ASP.NET Core Identity (in Infrastructure) while keeping the domain model cleanly encapsulated in `UserProfile`.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after feat/shared-kernel is merged
git checkout -b feat/module-users

# Build module
dotnet build src/Modules/Users/CarRental.Modules.Users/CarRental.Modules.Users.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, Money, FileReference, PhoneNumber, AggregateRoot).
- **External Dependencies:** ASP.NET Core Identity (stores authentication credentials in `AspNetUsers`).
- **Downstream Consumers of Contracts:** 
  - Booking (`IUsersApi.GetRenterEligibilityAsync`, `GetUserSummaryAsync`).
  - Fleet (`IUsersApi.IsVerifiedOwnerAsync`).
  - Notifications (`UserRegistered`, `IdentityReviewed`, `DriverLicenseReviewed`, `UserSuspended`).
  - Reporting (`UserRegistered`).

---

## 4. Contracts Project (`CarRental.Modules.Users.Contracts`)
### 4.1 Synchronous Interface (`IUsersApi`)
```csharp
public interface IUsersApi
{
    Task<Result<RenterEligibilityDto>> GetRenterEligibilityAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserSummaryDto>> GetUserSummaryAsync(Guid userId, CancellationToken ct = default);
    Task<bool> IsVerifiedOwnerAsync(Guid userId, CancellationToken ct = default);
}
```

### 4.2 DTOs
- `RenterEligibilityDto(bool IsEligible, IReadOnlyList<string> IneligibilityReasons)`
- `UserSummaryDto(Guid UserId, string FullName, string Email, string? PhoneNumber, string? AvatarUrl, bool IsOwner, bool IsVerified)`

### 4.3 Published Integration Events (4 Published Events)
- `UserRegistered(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, string Email, string FullName)`
- `IdentityReviewed(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, bool Approved, string? Reason)`
- `DriverLicenseReviewed(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, bool Approved, string? Reason)`
- `UserSuspended(Guid EventId, DateTimeOffset OccurredAt, Guid UserId, string Reason)`

*(Note: `UserBecameOwner` and `UserReinstated` are internal domain events).*

---

## 5. Domain Layer (`Entities/`, `Enums/`, `ValueObjects/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct UserId(Guid Value)`
- `UserRole` (`Renter`, `Owner`)
- `UserStatus` (`Active`, `Suspended`, `Banned`)
- `DocumentStatus` (`NotSubmitted`, `Pending`, `Verified`, `Rejected`)
- `LicenseClass` (`A1`, `A2`, `B1`, `B2`, `C`, `D`, `E`, `FC`)

### 5.2 Value Objects & Child Entities
- `PayoutAccount(string BankName, string AccountNumber, string AccountHolderName)`
- `IdentityDocument` (Child Entity):
  - `CitizenIdNumber` (masked: `********1234`), `FileReference FrontImage`, `FileReference BackImage`, `FileReference? SelfieImage`.
  - `DocumentStatus Status`, `Guid? ReviewedBy`, `DateTimeOffset? ReviewedAt`, `string? RejectReason`.
  - Rule: Sensitive files must have `IsPrivate = true`.
- `DriverLicense` (Child Entity):
  - `string Number`, `LicenseClass Class`, `DateOnly ExpiresOn`, `FileReference Image`, `DocumentStatus Status`.

### 5.3 Aggregate Root: `UserProfile`
- Invariants & Encapsulation:
  - Private constructors with factory method `UserProfile.Register(...)`.
  - `SubmitIdentityDocument(...)`: Re-submission resets status to `Pending`.
  - `ApproveIdentity(adminId)` / `RejectIdentity(adminId, reason)`
  - `SubmitDriverLicense(...)`, `ApproveDriverLicense(...)`, `RejectDriverLicense(...)`
  - `BecomeOwner(PayoutAccount payout)`: requires Identity `Verified`.
  - `Suspend(reason, adminId)` / `Reinstate()`: Suspended users fail eligibility.
  - Business Query `GetRenterEligibility(DateTimeOffset now)`:
    - Identity must be `Verified`.
    - DriverLicense must be `Verified` and `ExpiresOn >= DateOnly.FromDateTime(now.DateTime)`.
    - `Status == UserStatus.Active`.
    - Age >= 21 years old.

---

## 6. Persistence & EF Core (`Data/`)
- `UsersDbContext`: `HasDefaultSchema("users")`.
- `IEntityTypeConfiguration<UserProfile>`:
  - `xmin` concurrency token.
  - Value conversions for `UserId`.
  - `OwnsOne` for `PayoutAccount`.
  - Child entity configurations for `IdentityDocument` and `DriverLicense`.
  - Seed admin and test accounts.
- Generate initial migration: `AddUsersInitialMigration`.

---

## 7. Application Services & Endpoints (`Services/`, `Endpoints/`)
- `UsersApiService`: Implements `IUsersApi`.
- Minimal API Endpoints (`MapGroup("/api/users")`):
  - `POST /register`
  - `GET /me` (requires Auth)
  - `PUT /me/profile`
  - `POST /me/identity` (upload CCCD images -> Cloudinary private)
  - `POST /me/driver-license`
  - `POST /me/become-owner`
  - Admin endpoints (`RequireAuthorization("Admin")`):
    - `POST /admin/identity/{id}/approve`
    - `POST /admin/identity/{id}/reject`
    - `POST /admin/driver-license/{id}/approve`
    - `POST /admin/driver-license/{id}/reject`
    - `POST /admin/users/{id}/suspend`

---

## 8. Step-by-Step Task Checklist
- [ ] Define Enums, `UserId`, `PayoutAccount` in `CarRental.Modules.Users`.
- [ ] Implement `IdentityDocument` and `DriverLicense` entities.
- [ ] Implement `UserProfile` aggregate root with all invariants and `GetRenterEligibility`.
- [ ] Implement `IUsersApi` and DTOs/Events in `CarRental.Modules.Users.Contracts`.
- [ ] Setup `UsersDbContext` and EF configurations with `HasDefaultSchema("users")`.
- [ ] Create EF Core migration for `users` schema.
- [ ] Implement Minimal API endpoints and FluentValidation rules.
- [ ] Unit tests for `UserProfile`:
  - `Register_ShouldInitializeWithRenterRoleAndActiveStatus`
  - `BecomeOwner_WithoutVerifiedIdentity_ShouldFail`
  - `GetRenterEligibility_WithExpiredLicense_ShouldReturnIneligible`
  - `GetRenterEligibility_Under21_ShouldReturnIneligible`
- [ ] Integration tests using Testcontainers PostgreSQL.
