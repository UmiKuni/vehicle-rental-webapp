# Users Module — Implementation Plan

> **Module Directory:** `src/Modules/Users`  
> **Projects:** `CarRental.Modules.Users.csproj`, `CarRental.Modules.Users.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-users`  
> **Phase:** 1 (Catalog & Core Foundation — Parallel Stream 1)  
> **PostgreSQL Schema:** `users`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.6 & 5)

> [!IMPORTANT]
> **Implementation Scope Boundary:**
> Current implementation scope is strictly restricted to **Contracts, Domain Entities, and EF Core mappings (Data/)**.
> - **In Scope:** Aggregate roots, child entities, value objects, domain events, strongly-typed IDs, Contracts (interfaces, DTOs, enums, integration events), DbContext, and IEntityTypeConfiguration<T>.
> - **Out of Scope (Do NOT build yet):** Application services, repositories, minimal API endpoints, controllers, and background jobs. Stop proceeding immediately after domain entities and EF Core data configurations are completed.
> - **EF Core Migrations Policy:** Do NOT run migrations during entity construction. Database migrations (dotnet ef migrations add) will only be generated after all entities across all modules have been fully implemented and verified.


---

## 0. Notification of Changes (Old Design vs. Reworked Source-of-Truth)

| Dimension | Previous (Old Design) | Reworked (Source of Truth from `Thiết kế hệ thống, CSDL và API`) |
|---|---|---|
| **Aggregate Root** | `UserProfile` (table `user_profiles`) | `User` (table `Users`) with `Id`, `Email` (unique), `Phone` (unique), `PasswordHash`, `Role` (`Guest`/`Customer`/`Owner`/`Admin`), `Status` (`Active`/`Banned`). |
| **Child Entities** | `DriverLicense`, `IdentityDocument` (with front/back/selfie `FileReference`), `PayoutAccount` VO | `CustomerProfile` (table `CustomerProfiles`, PK `UserId`), `OwnerProfile` (table `OwnerProfiles`, PK `UserId`, TaxCode, ApprovalStatus), `IdentityDocument` (table `IdentityDocuments`, Type: `CCCD`/`GPLX`, StorageKey, VerificationStatus). |
| **Auth & Tokens** | Unspecified token entity | `RefreshToken` (table `RefreshTokens`) for token rotation and revocation. |
| **Published Events** | 4 events (`UserRegistered`, `IdentityReviewed`, `DriverLicenseReviewed`, `UserSuspended`) | **0 published events** (per Section 5 event catalog; source business flows require zero async subscribers). |
| **Contracts** | `IUsersApi.GetRenterEligibilityAsync`, `GetUserSummaryAsync`, `IsVerifiedOwnerAsync` | `IUsersApi.GetBookingEligibilityAsync(userId)`, `IUsersApi.GetContactAsync(userId)`. |
| **Table Naming** | snake_case tables | Retained snake_case tables (`users`, `customer_profiles`, `owner_profiles`, `identity_documents`, `refresh_tokens`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Users module manages authentication accounts, customer profiles, owner onboarding, citizen identity cards (CCCD) and driver licenses (GPLX) verification, and user ban/unban moderation. It integrates with ASP.NET Core Identity credential hashing while encapsulating domain logic in the `User` aggregate root.

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
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, AggregateRoot, Entity).
- **External Dependencies:** ASP.NET Core Identity password hasher.
- **Downstream Consumers of Contracts:** 
  - `Bookings`: Calls `IUsersApi.GetBookingEligibilityAsync(userId)` during booking placement.
  - `Notifications`: Calls `IUsersApi.GetContactAsync(userId)` (email, phone) for communication delivery.

---

## 4. Contracts Project (`CarRental.Modules.Users.Contracts`)

### 4.1 Synchronous Interface (`IUsersApi`)
```csharp
public interface IUsersApi
{
    Task<Result<BookingEligibilityDto>> GetBookingEligibilityAsync(Guid userId, CancellationToken ct = default);
    Task<Result<UserContactDto>> GetContactAsync(Guid userId, CancellationToken ct = default);
}
```

### 4.2 DTOs
- `BookingEligibilityDto(bool IsEligible, string? IneligibilityReason)`
- `UserContactDto(Guid UserId, string Email, string Phone, string FullName)`

### 4.3 Published Integration Events
- **0 Published Events** (Per Section 5 of `domain-model-entities-reworked.md`, all state transitions are internal or queried synchronously).

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct UserId(Guid Value)`
- `UserRole`: `Guest`, `Customer`, `Owner`, `Admin` (single enum value per source).
- `UserStatus`: `Active`, `Banned` [ASSUMPTION].
- `DocumentType`: `CCCD`, `GPLX`, `PassPort`.
- `DocumentVerificationStatus`: `Pending`, `Verified`, `Rejected` [ASSUMPTION].
- `OwnerApprovalStatus`: `Pending`, `Approved`, `Rejected`.

### 5.2 Child Entities
- **`CustomerProfile`** (Table `CustomerProfiles`, PK `UserId`):
  - Properties: `Guid UserId`, `string FullName`, `string Address`.
- **`OwnerProfile`** (Table `OwnerProfiles`, PK `UserId`):
  - Properties: `Guid UserId`, `string TaxCode`, `OwnerApprovalStatus ApprovalStatus`, `string? RejectReason`.
- **`IdentityDocument`** (Table `IdentityDocuments`):
  - Properties: `Guid Id`, `Guid UserId`, `DocumentType Type`, `string StorageKey`, `DocumentVerificationStatus VerificationStatus`, `string? RejectReason`.
  - Invariant: Sensitive documents accessed only via short-lived presigned URLs (NFR-05).
- **`RefreshToken`** (Table `RefreshTokens` [ASSUMPTION]):
  - Properties: `Guid Id`, `Guid UserId`, `string TokenHash`, `DateTimeOffset ExpiresAt`, `DateTimeOffset? RevokedAt`, `Guid? ReplacedByTokenId`, `string? DeviceInfo`, `DateTimeOffset CreatedAt`.

### 5.3 Aggregate Root: `User`
- Table `Users`:
  - `Guid Id`
  - `string Email` (unique, indexed)
  - `string Phone` (unique, indexed)
  - `string PasswordHash`
  - `UserRole Role` (`Guest`/`Customer`/`Owner`/`Admin`)
  - `UserStatus Status` (`Active`/`Banned`)
  - Child collections / navigation properties: `CustomerProfile? CustomerProfile`, `OwnerProfile? OwnerProfile`, `IReadOnlyCollection<IdentityDocument> IdentityDocuments`.
- Domain Behavior & Invariants:
  ```csharp
  static Result<User> Register(string email, string phone, string passwordHash); // Role = Customer, Status = Active
  Result UpdateProfile(string fullName, string address);
  Result AddIdentityDocument(DocumentType type, string storageKey); // Replaces doc of same type -> VerificationStatus = Pending
  Result SubmitOwnerOnboarding(string taxCode, IEnumerable<string> businessDocKeys); // OwnerProfile Pending
  Result ApproveOwner(Guid adminId); // Approved => Role = Owner
  Result RejectOwner(Guid adminId, string reason);
  Result VerifyDocument(Guid docId, Guid adminId, bool ok, string? reason);
  Result Ban(Guid adminId, string reason); // Status = Banned
  Result Unban(Guid adminId); // Status = Active
  BookingEligibilityDto GetBookingEligibility(); // Active + FullName present + CCCD and GPLX Verified
  ```

---

## 6. Persistence & EF Core (`Data/`)
- `UsersDbContext`: `HasDefaultSchema("users")`.
- Table mapping (snake_case):
  - `users` (Aggregate Root with `xmin` row version)
  - `customer_profiles` (PK/FK `user_id`)
  - `owner_profiles` (PK/FK `user_id`)
  - `identity_documents` (FK `user_id`, unique index on `(user_id, type)`)
  - `refresh_tokens` (FK `user_id`, index on `token_hash`)
- EF Core entity configurations in `Data/Configurations/`.

---

## 7. Application Services & Endpoints (`Services/`, `Endpoints/`)
- `UsersApiService`: Implements `IUsersApi`.
- Minimal API Endpoints (`MapGroup("/api/users")`):
  - `POST /auth/register`
  - `POST /auth/login`
  - `PUT /me/profile`
  - `POST /me/identity-docs` (returns presigned upload URL)
  - `POST /me/owner-onboarding`
  - `PUT /{id}/ban` (Admin policy, records audit entry)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `User` aggregate root and child entities (`CustomerProfile`, `OwnerProfile`, `IdentityDocument`, `RefreshToken`).
- [ ] Implement `IUsersApi` and DTOs in `CarRental.Modules.Users.Contracts`.
- [ ] Setup `UsersDbContext` with schema `users` and snake_case tables.
- [ ] Add domain unit tests for `User` invariants, registration, document replacement, owner onboarding approval, and booking eligibility.
- [ ] Verify zero EF/ASP.NET dependencies in `Entities/` via `DomainEncapsulationTests`.
