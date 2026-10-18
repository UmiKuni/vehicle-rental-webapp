# Reviews Module — Implementation Plan

> **Module Directory:** `src/Modules/Reviews`  
> **Projects:** `CarRental.Modules.Reviews.csproj`, `CarRental.Modules.Reviews.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-reviews`  
> **Phase:** 3 (Physical Fulfillment & Trust — Parallel Stream 2)  
> **PostgreSQL Schema:** `reviews`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.9 & 5)

---

## 1. Objectives & Scope
The Reviews module governs mutual feedback between renters and owners following trip completion. It controls a 14-day review window, 1-to-5 star ratings, sub-category scoring, owner replies, and admin moderation.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 2 contracts are available
git checkout -b feat/module-reviews

# Build module
dotnet build src/Modules/Reviews/CarRental.Modules.Reviews/CarRental.Modules.Reviews.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Rating, Result, Guard, AggregateRoot).
- **Consumes Events from:**
  - `Bookings`: `TripCompleted` (triggers opening of a new `ReviewWindow`).
- **Downstream Consumers of Reviews Contracts:**
  - `Search`: Consumes `ReviewPublished` and `ReviewHidden` to update average rating and review counts.
  - `Reporting`, `Notifications`.

---

## 4. Contracts Project (`CarRental.Modules.Reviews.Contracts`)
### 4.1 Synchronous Interface (`IReviewsApi`)
```csharp
public interface IReviewsApi
{
    Task<Result<VehicleRatingSummaryDto>> GetVehicleRatingSummaryAsync(Guid vehicleId, CancellationToken ct = default);
}
```

### 4.2 Published Integration Events (2 Published Events)
- `ReviewPublished(Guid EventId, DateTimeOffset OccurredAt, Guid ReviewId, Guid BookingId, Guid VehicleId, Guid ReviewerId, byte Rating, string Comment)`
- `ReviewHidden(Guid EventId, DateTimeOffset OccurredAt, Guid ReviewId, Guid AdminId, string Reason)`

*(Note: `ReviewReplied` is an internal domain event).*

---

## 5. Domain Layer (`Entities/`, `Enums/`, `ValueObjects/`)

### 5.1 Enums & Value Objects
- `ReviewDirection` (`RenterToOwnerAndVehicle`, `OwnerToRenter`)
- `ReviewStatus` (`Published`, `Hidden`)
- `SubRatings(byte Cleanliness, byte Accuracy, byte Communication)`
- `OwnerReply(string Text, DateTimeOffset RepliedAt)`

### 5.2 Aggregate Root: `ReviewWindow`
- Properties:
  - `Guid BookingId` (Unique)
  - `Guid VehicleId`, `Guid RenterId`, `Guid OwnerId`
  - `DateTimeOffset OpensAt`, `DateTimeOffset ExpiresAt` (OpensAt + 14 days)
  - `bool RenterReviewed`, `bool OwnerReviewed`
- Methods:
  - `bool CanReview(Guid userId, DateTimeOffset now)`
  - `Result MarkReviewed(Guid userId)`

### 5.3 Aggregate Root: `Review`
- Properties:
  - `Guid Id`
  - `Guid BookingId`, `Guid VehicleId`, `Guid ReviewerId`, `Guid RevieweeId`
  - `ReviewDirection Direction`
  - `Rating Rating` (1..5)
  - `SubRatings? SubRatings`
  - `string Comment` (<= 2000 characters)
  - `ReviewStatus Status`
  - `OwnerReply? OwnerReply`
  - `DateTimeOffset CreatedAt`, `DateTimeOffset? EditedAt`
- Invariants & Methods:
  - `static Result<Review> Create(ReviewWindow window, ...)`: Validates that window is still open and user has not already submitted.
  - `Result Edit(string newComment, Rating newRating, DateTimeOffset now)`: Allowed only within 24 hours of creation.
  - `Result Reply(Guid ownerId, string text, DateTimeOffset now)`: Vehicle owner only, exactly one reply allowed.
  - `Result Hide(Guid adminId, string reason, DateTimeOffset now)`
  - `Result Unhide(Guid adminId)`

---

## 6. Persistence & EF Core (`Data/`)
- `ReviewsDbContext`: `HasDefaultSchema("reviews")`.
- `IEntityTypeConfiguration<Review>`:
  - Unique index on `(BookingId, Direction)` — strictly one review per direction per booking.
  - Index on `VehicleId`.
  - Owned entities for `Rating`, `SubRatings`, `OwnerReply`.
- `IEntityTypeConfiguration<ReviewWindow>`:
  - Unique index on `BookingId`.
- Migration: `AddReviewsInitialMigration`.

---

## 7. Event Handlers & Endpoints (`EventHandlers/`, `Endpoints/`)
- `TripCompletedHandler`: Consumes `TripCompleted` from Bookings -> Creates a new `ReviewWindow`.
- Minimal API Endpoints (`MapGroup("/api/reviews")`):
  - `GET /vehicles/{vehicleId}` (List published reviews)
  - `POST /` (Submit review)
  - `PUT /{id}` (Edit within 24h)
  - `POST /{id}/reply` (Owner reply)
  - Admin endpoints:
    - `POST /admin/reviews/{id}/hide`
    - `POST /admin/reviews/{id}/unhide`

---

## 8. Step-by-Step Task Checklist
- [ ] Implement Enums, `SubRatings`, `OwnerReply` value objects.
- [ ] Implement `ReviewWindow` aggregate root.
- [ ] Implement `Review` aggregate root with 24h edit window and single reply invariant.
- [ ] Implement `IReviewsApi` and integration events in `CarRental.Modules.Reviews.Contracts`.
- [ ] Configure `ReviewsDbContext` and EF Core mappings.
- [ ] Create EF Core migration for `reviews` schema.
- [ ] Implement Wolverine handler for `TripCompleted`.
- [ ] Build Minimal API endpoints.
- [ ] Unit tests:
  - `Create_WhenReviewWindowExpired_ShouldFail`
  - `Create_DuplicateReviewForSameDirection_ShouldFail`
  - `Edit_After24Hours_ShouldFail`
  - `Reply_ByNonOwner_ShouldFail`
- [ ] Testcontainers integration tests.
