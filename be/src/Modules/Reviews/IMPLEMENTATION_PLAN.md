# Reviews Module — Implementation Plan

> **Module Directory:** `src/Modules/Reviews`  
> **Projects:** `CarRental.Modules.Reviews.csproj`, `CarRental.Modules.Reviews.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-reviews`  
> **Phase:** 3 (Physical Fulfillment & Trust — Parallel Stream 2)  
> **PostgreSQL Schema:** `reviews`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.9 & 5)

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
| **Aggregate Roots** | `Review` with complex multi-aspect ratings and `ReviewWindow` aggregate root | Two distinct aggregates: `Review` (table `Reviews`) and `RatingSummary` (table `RatingSummaries`). |
| **Review Properties** | Multi-category scores (cleanliness, comfort, communication) | Simplified single `Rating` (1..5 integer) matching source DB schema. |
| **Local Read Model** | None | `ReviewableBooking` (table `ReviewableBookings`, `BookingId`, `VehicleId`, `CustomerId`) created from `BookingCompleted` to enforce "only completed bookings can be reviewed". |
| **Published Events** | 2 events (`ReviewPublished`, `ReviewHidden`) | **1 published event**: `RatingSummaryUpdated` (carries `VehicleId`, `AverageRating`, `TotalReviews` for Search). |
| **Table Naming** | snake_case tables | Retained snake_case tables (`reviews`, `rating_summaries`, `reviewable_bookings`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Reviews module manages renter post-trip feedback on vehicles. It enforces the rule that only completed rentals can be reviewed, computes real-time rating summaries (`RatingSummary`), and provides admin moderation (`Published` vs. `Hidden`).

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
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, AggregateRoot, Entity).
- **Consumes Events from:**
  - `Bookings`: `BookingCompleted` -> Creates local `ReviewableBooking`.
- **Downstream Consumers of Reviews Contracts:**
  - `Search`: Consumes `RatingSummaryUpdated` to synchronize vehicle search document card ratings.

---

## 4. Contracts Project (`CarRental.Modules.Reviews.Contracts`)

### 4.1 Synchronous Interface
*(No synchronous interface required; read model queries are internal and sync is event-driven).*

### 4.2 Published Integration Events (1 Published Event)
- `RatingSummaryUpdated(Guid EventId, DateTimeOffset OccurredAt, Guid VehicleId, decimal AverageRating, int TotalReviews)`

---

## 5. Domain Layer (`Entities/`, `Enums/`)

### 5.1 Enums & Strongly-Typed IDs
- `readonly record struct ReviewId(Guid Value)`
- `ReviewStatus`: `Published`, `Hidden`.

### 5.2 Local Read Model
- **`ReviewableBooking`** (Table `ReviewableBookings`):
  - Properties: `Guid BookingId` (PK), `Guid VehicleId`, `Guid CustomerId`, `DateTimeOffset CompletedAt`, `bool HasReviewed`.

### 5.3 Aggregate Root: `Review`
- Table `Reviews`:
  - `Guid Id`
  - `Guid BookingId` (unique, indexed)
  - `Guid VehicleId` (indexed)
  - `Guid CustomerId` (indexed)
  - `int Rating` (1..5)
  - `string Content`
  - `ReviewStatus Status` (`Published`/`Hidden`)
  - `DateTimeOffset CreatedAt`
- Domain Methods & Invariants:
  ```csharp
  static Result<Review> Create(ReviewableBooking booking, Guid customerId, int rating, string content, DateTimeOffset now);
  Result Hide(Guid adminId, string reason);
  Result Restore(Guid adminId);
  ```
- Invariants:
  - Rating must be between 1 and 5.
  - Exactly one review per booking.
  - Only the booking's verified customer can submit a review.
  - Only `Published` reviews count toward `RatingSummary`.

### 5.4 Aggregate Root: `RatingSummary`
- Table `RatingSummaries`:
  - `Guid VehicleId` (Aggregate Root PK)
  - `decimal AverageRating`
  - `int TotalReviews`
- Domain Methods:
  ```csharp
  void AddRating(int rating);
  void RemoveRating(int rating); // Called when a review is hidden
  ```

---

## 6. Persistence & EF Core (`Data/`)
- `ReviewsDbContext`: `HasDefaultSchema("reviews")`.
- Table mapping (snake_case):
  - `reviews` (Aggregate Root with `xmin` row version)
  - `rating_summaries` (Aggregate Root with `xmin` row version)
  - `reviewable_bookings` (PK `booking_id`)
- Unique index on `BookingId` in `Reviews`.

---

## 7. Event Handlers & Endpoints (`EventHandlers/`, `Endpoints/`)
- `BookingCompletedHandler`: Inserts `ReviewableBooking`.
- Minimal API Endpoints (`MapGroup("/api/reviews")`):
  - `POST /` (submit review for completed booking)
  - `GET /vehicles/{id}` (fetch published reviews + summary)
  - `PUT /{id}/moderate` (Admin policy: hide or restore review, updates `RatingSummary` and emits `RatingSummaryUpdated`)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `Review` and `RatingSummary` aggregate roots, and `ReviewableBooking` read model.
- [ ] Implement `RatingSummaryUpdated` integration event in `CarRental.Modules.Reviews.Contracts`.
- [ ] Setup `ReviewsDbContext` with schema `reviews`.
- [ ] Add domain unit tests for rating range invariants, average calculation, and moderation impact.
- [ ] Verify zero EF/ASP.NET dependencies in `Entities/` via `DomainEncapsulationTests`.
