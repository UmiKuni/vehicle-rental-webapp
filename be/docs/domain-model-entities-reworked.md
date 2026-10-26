# Domain Model Design (Rich Domain Model) for All Modules

> **Audience:** the sub-agents implementing the backend. Every agent reads **Sections 0–2** first, then only its own module(s) in Section 4 plus the Contracts of the modules it depends on.
>
> **Source of truth:** `Thiết kế hệ thống, CSDL và API` (the DB schemas in section 1 and the REST API in section 2 of that document, plus its architecture part: modular monolith, .NET 9, PostgreSQL + PostGIS, one EF Core DbContext per module, Wolverine outbox).

---

## 0. How to read this document

Three tags are used. **Anything without a tag comes directly from the source document.**

| Tag | Meaning | What the agent does |
|---|---|---|
| *(no tag)* | Table, column, enum value, endpoint, event name or business rule (BR-xx, NFR-xx) that exists in the source | Implement exactly, keep names |
| **[ASSUMPTION]** | The source is silent but the flow cannot work without it | Implement as written; keep values in configuration; listed again in Section 9 |
| **[EXT]** | Optional idea beyond the source | **Do not implement** unless the team decides so (Section 10) |

**Mapping rule:** each source table becomes an aggregate root, a child entity or a read model; each source column becomes a property with the same name (PascalCase) and enum values are kept verbatim. The Rich Domain Model adds behavior and invariants on top; it does **not** add columns except those tagged **[ASSUMPTION]**.

---

## 1. Common Conventions (mandatory for every module)

### 1.1 Rich Domain Model principles

1. **No anemic entities.** Every state change goes through a business method on the Aggregate Root (`Approve()`, `Cancel()`, `PlaceHold()`...). `public set;` is forbidden.
2. **Invariants live in the domain**, not in Application services or endpoints. Application layer only: load aggregate → call method → save → dispatch.
3. **Private constructor + static factory** `Create(...)` returning `Result<T>` (or `DomainException` for programmer errors). Add a private parameterless constructor for EF Core.
4. **Collections** inside an aggregate: `private readonly List<T> _items`, exposed as `IReadOnlyCollection<T>`.
5. **Domain events** are raised with `Raise(...)`. An event is converted to an **integration event** (Wolverine outbox, same transaction as `SaveChanges`) **only if another module needs it**; Section 5 is the single source of truth for published events. Everything else stays internal.
6. **No `DateTime.UtcNow` / `Guid.NewGuid()` inside the domain:** pass `DateTimeOffset now` (from `TimeProvider`) into methods; IDs use `Guid.CreateVersion7()` in factories.
7. **Strongly-typed IDs** for aggregate roots (`readonly record struct BookingId(Guid Value)`) with EF value converters.
8. **Value Objects** are immutable records that self-validate; map with `OwnsOne`/`ComplexProperty`/JSONB.
9. **Enums** are stored as strings.
10. **One aggregate = one transaction.** Reference other aggregates and other modules by ID only.

### 1.2 Modular monolith constraints (from the source architecture)

- One PostgreSQL schema per module: `booking`, `pricing`, `availability`, `payments`, `handover`, `users`, `fleet`, `search`, `reviews`, `support`, `notifications`, `audit`, `reporting`.
- **No** foreign keys or joins across schemas; only IDs. (Source: "Foreign Key" columns point only to tables of the **same** schema.)
- A module references another module only through its `*.Contracts` project (NetArchTest fails the build otherwise).
- **Synchronous calls** (interfaces in Contracts) where an immediate answer is needed. Source-defined: Booking → Pricing (snapshot quote), Booking → Availability (hold), Booking → Users (verified profile). Additional read-only calls added by this design are tagged **[ASSUMPTION]** (Section 3).
- **Integration events via outbox** for everything else; every handler must be **idempotent**.
- Admin is **not a module**: admin endpoints live in the module that owns the resource and use the `Admin` policy.

### 1.3 Project structure for each module `X` (Layered Architecture)

Each module adopts a **pragmatic Layered Architecture** with **two projects** per module (instead of Clean Architecture's 4–5 projects), matching `Architecture Analysis` and the existing solution layout:

```
src/
  Host/
    CarRental.Api/                            # ASP.NET Core host, routing, JWT auth, Wolverine configuration
  Shared/
    CarRental.BuildingBlocks/                 # Shared Kernel (Entity, AggregateRoot, Result, Guard, VO: Money, DateTimeRange)
  Modules/{Module}/
    CarRental.Modules.{Module}.Contracts/     # PUBLIC project: synchronous I{Module}Api, DTOs, Enums, Integration Events
    CarRental.Modules.{Module}/               # INTERNAL project (internal by default, single assembly):
      Endpoints/                              # Minimal API route groups & endpoint handlers (IEndpointRouteBuilder extensions)
      Services/                               # Business workflows, application orchestration, background/cron jobs, adapters
      Entities/                               # Rich Domain Model: aggregates, entities, value objects, domain events, status machines (no EF/ASP.NET dependencies)
      Data/                                   # {Module}DbContext, IEntityTypeConfiguration<T>, EF Core migrations
      EventHandlers/                          # Wolverine integration event handlers consuming other modules' events
tests/
  CarRental.ArchitectureTests/                # NetArchTest guardrails (encapsulation, no cross-module internal references, no public setters)
  CarRental.BuildingBlocks.Tests/             # Shared kernel unit tests
  # Module unit & integration tests (Testcontainers PostgreSQL)
```

**Key Architectural Rules:**
1. **Dependencies point downward:** `Endpoints/` → `Services/` → `Entities/` & `Data/`.
2. **Internals stay `internal`:** Only `CarRental.Modules.{Module}.Contracts` is `public`. All internal implementation types inside `CarRental.Modules.{Module}` remain `internal` so other modules cannot reference or access them.
3. **Pure domain isolation:** Types under `Entities/` (or `Domain/`) must remain pure C# and have zero dependencies on EF Core (`Microsoft.EntityFrameworkCore`) or ASP.NET Core (`Microsoft.AspNetCore`). This invariant is strictly enforced by `DomainEncapsulationTests`.
4. **No cross-module internal references:** Modules may only reference other modules' `*.Contracts` projects and `CarRental.BuildingBlocks`. Enforced by `ModuleEncapsulationTests`.


### 1.4 EF Core / PostgreSQL conventions

- One `DbContext` per module with `HasDefaultSchema("<schema>")`; table names use `snake_case` (e.g. `booking.bookings`, `users.customer_profiles`, `fleet.vehicles`).
- `Timestamptz` for all times (UTC). Money columns are `Decimal(18,0)` (VND). **[ASSUMPTION]** business rules about "weekday/weekend" and "per day" use Vietnam time (UTC+7).
- Optimistic concurrency on every aggregate root (`xmin` mapped as row version).
- Double-booking is prevented by a **PostgreSQL exclusion constraint** on `tstzrange` (BR-05), in addition to the domain check.
- Geography/geometry: PostGIS (`Location` is `Geometry` per source), GIST index.
- Files never live in the DB; only `StorageKey`. Identity documents are reachable only through short-lived presigned URLs (NFR-05).

### 1.5 Testing conventions

Every aggregate has pure unit tests (no DB) for: every valid transition, every forbidden transition, every invariant, and every raised event. Naming: `Method_Condition_ExpectedResult`. Event handlers get an "invoked twice" idempotency test.

---

## 2. Shared Kernel (write first)

> The Shared Kernel (`src/Shared/CarRental.BuildingBlocks`) contains only building blocks and **no business logic**. All modules may reference `CarRental.BuildingBlocks` in addition to other modules' `*.Contracts` projects (enforced by NetArchTest rules).

```csharp
public abstract class Entity<TId> where TId : notnull { public TId Id { get; protected set; } = default!; /* equality by Id */ }

public interface IDomainEvent { Guid EventId { get; } DateTimeOffset OccurredAt { get; } }

public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _events = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;
    protected void Raise(IDomainEvent e) => _events.Add(e);
    public void ClearDomainEvents() => _events.Clear();
}

public abstract record ValueObject;
public sealed record Error(string Code, string Message);        // e.g. "booking.invalid_transition"
public readonly record struct Result { /* IsSuccess, Error */ }
public readonly record struct Result<T> { /* IsSuccess, Value, Error */ }
public class DomainException(string code, string message) : Exception(message);
public static class Guard { /* AgainstNull, AgainstNullOrWhiteSpace, AgainstOutOfRange, Against(bool, code, msg) */ }
```

| Value Object | Shape | Rules |
|---|---|---|
| `Money` | `decimal Amount` (VND) | ≥ 0, no fractional VND; `Add`, `Subtract` (never negative), `Multiply(decimal)`, `Percent(decimal)`; persisted as one `Decimal` column |
| `TimeRange` | `Start`, `End` (`DateTimeOffset`) | `End > Start`; `Overlaps`, `Contains`, `Days` (rounded up to whole 24h days **[ASSUMPTION]**); maps to `tstzrange` in Availability |
| `GeoPoint` | `Latitude`, `Longitude` | valid ranges; maps to PostGIS geometry (SRID 4326) |

---

## 3. Module Map, Ownership and Contracts

| Group | Module | Aggregate roots / read models | Owner (2-person split) |
|---|---|---|---|
| Rental flow | **Booking** | `Booking` | **A** |
| | **Pricing** | `PricingPlan` | A |
| | **Availability** | `VehicleCalendar` | A |
| | **Payments** | `Payment`, `Deposit` | A |
| | **Handover** | `Handover` | A |
| Catalog & people | **Users** | `User` | **B** |
| | **Fleet** | `Vehicle` | B |
| | **Search** | `VehicleSearchDocument` (read model) | B |
| | **Reviews** | `Review`, `RatingSummary` | B |
| Support & platform | **Support** | `SupportTicket` | B |
| | **Notifications** | `Notification` | B |
| | **Audit** | `AuditLogEntry` | B |
| | **Reporting** | `DailyBookingStats` (read model) | A |

**Why this split:** Booking calls Pricing and Availability synchronously, and Payments/Handover run entirely on Booking's state machine, so one person owns that whole chain (A). Users, Fleet and Search share the vehicle/user data, and Reviews/Support/Notifications/Audit mostly consume events (B). B also builds the API host and authentication.

**Synchronous contracts (agree on these on day 1):**

| Contract | Provider → Consumer | Source? |
|---|---|---|
| `IPricingApi.GetQuoteAsync(vehicleId, period)` | Pricing → Booking | Source |
| `IAvailabilityApi.PlaceHoldAsync / ConfirmHoldAsync / ReleaseHoldAsync` | Availability → Booking | Source (hold); confirm/release **[ASSUMPTION]** as sync too |
| `IUsersApi.GetBookingEligibilityAsync(userId)` | Users → Booking | Source ("verified profile") |
| `IFleetApi.GetVehicleSummaryAsync(vehicleId)` → `OwnerId`, `Status` | Fleet → Booking, Pricing, Availability | **[ASSUMPTION]** (ownership checks) |
| `IBookingApi.IsParticipantAsync(bookingId, userId)` | Booking → Support | **[ASSUMPTION]** |
| `IUsersApi.GetContactAsync(userId)` (email, phone) | Users → Notifications | **[ASSUMPTION]** |

**Delivery order:** phase 0 (A: `CarRental.BuildingBlocks` + outbox + NetArchTest; B: API host + auth) → phase 1 (A: Pricing, Availability; B: Users, Fleet) → phase 2 (A: Booking, Payments; B: Search, Reviews) → phase 3 (A: Handover, Reporting; B: Support, Notifications, Audit). In phase 2 A uses fakes of `IUsersApi`/`IFleetApi` until B delivers.

---

## 4. Module Design

> Each module lists: aggregate structure, behavior, invariants, events, consumed events and the API it serves (all endpoints are from the source).

### 4.1 Booking — schema `booking`

**Aggregate root `Booking`** (table `Bookings`): `Id`, `CustomerId`, `OwnerId`, `VehicleId`, `Status`, `StartTime`, `EndTime`, `PickupLocation` (text), `CreatedAt`, `UpdatedAt`.
**Child entities:**
- `BookingLineItem` (`Id`, `Type`: `Rent`/`Deposit`/`CleaningFee`/`OverKmFee`, `Amount`).
- `CancellationPolicySnapshot` (1:1, PK `BookingId`, `PolicyDetails` JSONB) — immutable copy so later changes do not affect the booking (BR-08).
- `BookingStatusHistory` (`Id`, `Status`, `ChangedAt`, `Reason`) — append-only; one row per transition.
**[ASSUMPTION]** extra property `PaymentDeadline` (see state machine) and, inside the snapshot JSON, `KmLimitPerDay` and `OverKmFeePerKm`.

**State machine**

```
Pending ──Approve──► Confirmed ──MarkPaid──► Paid ──StartTrip──► InProgress ──CompleteTrip──► Completed
   │                    │                      │
   ├─Reject──► Rejected ├─Expire──► Expired    └─Cancel──► Cancelled
   ├─Expire──► Expired  └─Cancel──► Cancelled        (Admin cancel: any non-terminal state)
   └─Cancel──► Cancelled
```

`Pending` is created with a calendar `Hold` of at most 12h (BR-07). Approval turns the hold into `Booked`. `Paid` is set by the `PaymentSucceeded` event. **[ASSUMPTION]** `InProgress`/`Completed` are driven by `HandoverConfirmed` (Pickup / Return). **[ASSUMPTION]** `Confirmed → Expired` after `PaymentDeadline` (default 2h after approval), because a `Booked` calendar entry never expires on its own.

**Behavior**
```csharp
static Result<Booking> Request(customerId, ownerId, vehicleId, TimeRange period, string pickupLocation,
                               IReadOnlyList<LineItemDraft> quoteLines, CancellationPolicySnapshot policy, DateTimeOffset now);
Result Approve(Guid ownerId, DateTimeOffset now);                 // Pending -> Confirmed (owner only)
Result Reject(Guid ownerId, string reason, DateTimeOffset now);   // Pending -> Rejected
Result MarkPaid(Guid paymentId, DateTimeOffset now);              // Confirmed -> Paid (from PaymentSucceeded)
Result<CancellationOutcome> Cancel(Guid actorId, string reason, DateTimeOffset now); // customer cancel; computes penalty from snapshot
Result AdminCancel(Guid adminId, string reason, DateTimeOffset now);
Result Expire(DateTimeOffset now);                                // Pending (hold expired) or Confirmed (payment deadline)
Result StartTrip(DateTimeOffset now);                             // Paid -> InProgress (HandoverConfirmed/Pickup)
Result CompleteTrip(DateTimeOffset now);                          // InProgress -> Completed (HandoverConfirmed/Return)
Result AddOverKmFee(Money amount);                                // [ASSUMPTION] adds an OverKmFee line on completion
```
`CancellationOutcome` = `PenaltyAmount`, `RefundableAmount` **[ASSUMPTION]**: `PolicyDetails` is a list of tiers `(hoursBeforeStart, penaltyPercent)` applied to the `Rent` line.

**Invariants:** `CustomerId != OwnerId`; `StartTime` in the future at request and `EndTime > StartTime`; must contain at least one `Rent` and one `Deposit` line; line items and policy snapshot never change after creation (except `OverKmFee`); only the booking's owner approves/rejects; only its customer cancels; `Completed/Cancelled/Rejected/Expired` are terminal; every transition writes a `BookingStatusHistory` row and sets `UpdatedAt`.

**Creation flow** (`POST /api/bookings`, application service `BookingPlacementService`): `IUsersApi` eligibility → `IFleetApi` (owner, vehicle must be `Published`) → `IPricingApi` quote → `IAvailabilityApi.PlaceHold` (12h) → `Booking.Request` → save. If saving fails after the hold, release the hold. Approve/Reject/Cancel/Expire call `ConfirmHold`/`ReleaseHold` synchronously.
**Background job:** `ExpireOverdueBookingsJob`.

**API:** `POST /api/bookings`, `GET /api/bookings/me`, `PUT /api/bookings/{id}/cancel`, `GET /api/bookings/owner`, `PUT /api/bookings/{id}/approve`, `PUT /api/bookings/{id}/reject`, `PUT /api/bookings/{id}/admin-cancel` (Admin).
**Published events:** `BookingRequested`, `BookingConfirmed` (carries payable amount and deposit part), `BookingPaid` (carries participants and period), `BookingRejected`, `BookingCancelled` (carries `PenaltyAmount`), `BookingExpired`, `BookingCompleted`.
**Consumes:** `PaymentSucceeded` → `MarkPaid`; `HandoverConfirmed` → `StartTrip` / `CompleteTrip`.

---

### 4.2 Pricing — schema `pricing`

**Aggregate root `PricingPlan`** (table `PricingPlans`): `Id`, `VehicleId` (unique, indexed), `WeekdayRate`, `WeekendRate`, `DepositAmount`, `KmLimit` (per day).
**Child entities:**
- `FeeRule` (`Id`, `Type`: `OverKm`/`Cleaning`, `Amount`) — at most one per `Type`.
- `PricingPlanVersion` (`Id`, `VehicleId`, `Config` JSONB, `CreatedAt`) — one row appended on **every** change for audit (NFR-13); never loaded with the aggregate.

**Behavior**
```csharp
static Result<PricingPlan> Create(vehicleId, weekdayRate, weekendRate, depositAmount, kmLimit, now);
Result Update(weekdayRate, weekendRate, depositAmount, kmLimit, now);   // appends a PricingPlanVersion, raises PricingUpdated
Result SetFeeRule(FeeType type, Money amount, now);                     // upsert by type, appends a version
Result RemoveFeeRule(FeeType type, now);
Result<Quote> CalculateQuote(TimeRange period);                         // pure, no side effects, nothing persisted
```
`Quote` (value object, **not stored**; the booking stores the snapshot as `BookingLineItems`): `Lines` = `Rent` (sum per day: weekend days use `WeekendRate`, other days `WeekdayRate`, **[ASSUMPTION]** Saturday/Sunday, UTC+7, partial day counts as a full day), `Deposit` (`DepositAmount`), `CleaningFee` (if a `Cleaning` rule exists); `Total`; `KmLimitPerDay`, `OverKmFeePerKm` (information for the snapshot, charged later); **[ASSUMPTION]** `CancellationPolicy` taken from platform configuration (the source does not say where the policy comes from).

**Invariants:** rates > 0, `DepositAmount ≥ 0`, `KmLimit > 0`, fee amounts ≥ 0; exactly one plan per vehicle; only the vehicle's owner may change the plan (checked via `IFleetApi`).
**API:** `POST /api/pricing/quotes` (body: `vehicleId`, `startTime`, `endTime`; the server computes the final price), `PUT /api/pricing/vehicles/{id}` (weekend rate, over-km fee, cleaning fee, ...).
**Published events:** `PricingUpdated` (carries `VehicleId`, `PriceFrom = min(WeekdayRate, WeekendRate)`).

---

### 4.3 Availability — schema `availability`

**Aggregate root `VehicleCalendar`** (table `VehicleCalendars`): `VehicleId` (PK), `LastSyncedAt`.
**Child entity `CalendarEntry`** (table `CalendarEntries`): `Id`, `VehicleId` (indexed), `Type` (`Booked`/`Hold`/`OwnerBlocked`/`Maintenance`), `TimeRange` (`tstzrange`), `ExpiresAt` (null for `Booked`/blocks; ≤ 12h for `Hold`, BR-07). **[ASSUMPTION]** `BookingId` (nullable) to find the entries of a booking.
Load only the entries overlapping the range being processed, never the whole history.

**Behavior**
```csharp
Result PlaceHold(Guid bookingId, TimeRange range, TimeSpan ttl, DateTimeOffset now);   // ttl <= 12h
Result ConfirmHold(Guid bookingId, DateTimeOffset now);          // Hold -> Booked, ExpiresAt = null
Result Release(Guid bookingId, DateTimeOffset now);              // removes the booking's entries
Result Block(EntryType type /*OwnerBlocked|Maintenance*/, TimeRange range, DateTimeOffset now);
Result Unblock(Guid entryId, DateTimeOffset now);
int ExpireHolds(DateTimeOffset now);                             // removes expired holds
bool IsAvailable(TimeRange range, DateTimeOffset now);
```
Every mutation sets `LastSyncedAt`.
**Invariants:** active entries never overlap (domain check **and** the Postgres exclusion constraint, BR-05); `Hold` always has `ExpiresAt` within 12h; `Booked`/`OwnerBlocked`/`Maintenance` have `ExpiresAt = null`; an expired `Hold` is not active; only `Hold` can be confirmed; owner blocks cannot overlap `Booked`/active `Hold` entries; only the vehicle's owner may block (via `IFleetApi`).
**Background job:** `ExpireHoldsJob` (every minute; safety-net cleanup, Booking enforces its own deadlines).
**API:** `GET /api/availability/vehicles/{id}/check` (`startTime`, `endTime`), `POST /api/availability/vehicles/{id}/blocks`.
**Published events:** none. **Consumes:** none (all calls are synchronous).

---

### 4.4 Payments — schema `payments`

**Aggregate root `Payment`** (table `Payments`): `Id`, `BookingId`, `Amount`, `Status` (`Pending`/`Success`/`Failed`), `GatewayTransactionId`, `Method` (`VNPay`/`Momo`/`Transfer`). **[ASSUMPTION]** `FailureReason`, `DepositPortion` (the part of `Amount` that is the deposit).
**Aggregate root `Deposit`** (table `Deposits`): `Id`, `BookingId`, `Amount`, `Status` (`Held`/`Released`/`Deducted`/`Forfeited`). **[ASSUMPTION]** `DeductedAmount`, `ReleaseAfter`, `IsFrozen`, `SettledAt`.
**Infrastructure entity `WebhookInbox`** (`Id` text = idempotency key, `ProcessedAt`, `Payload` JSONB): the webhook handler first inserts the id; a duplicate means "already processed, return 200".

**Behavior**
```csharp
// Payment
static Result<Payment> CreatePending(bookingId, Money amount, Money depositPortion, now); // from BookingConfirmed
Result Checkout(PaymentMethod method, IPaymentGateway gateway, now);   // returns gateway redirect/checkout data; Transfer = manual flow
Result MarkSuccess(string gatewayTransactionId, now);                  // Pending -> Success, raises PaymentSucceeded
Result MarkFailed(string gatewayTransactionId, string reason, now);    // Pending -> Failed, raises PaymentFailed
Result Cancel(string reason, now);                                     // booking cancelled/expired while Pending (stored as Failed)

// Deposit
static Deposit Hold(bookingId, Money amount);                          // created when the payment succeeds
void Freeze();                                                         // a ticket was opened
Result Release(now);                                                   // Held -> Released
Result Deduct(Money amount, now);                                      // Held -> Deducted (remainder returned)
Result Forfeit(now);                                                   // Held -> Forfeited
Result SettleByRuling(int depositToCustomerPercent, now);              // 100 => Released, 1..99 => Deducted, 0 => Forfeited
```
**Invariants:** `Amount > 0`; `Success`/`Failed` only from `Pending`; at most one `Success` per booking; `GatewayTransactionId` unique when set; deposit transitions only from `Held` and never exceed `Amount`; a frozen deposit is not auto-released.
**Gateway:** `IPaymentGateway` abstraction (VNPay, Momo sandbox adapters in Infrastructure); the domain never references a gateway SDK.
**Background job:** `ReleaseDueDepositsJob` **[ASSUMPTION]** releases deposits of completed bookings after `ReleaseAfter` (default 48h after completion) unless `IsFrozen`.
**API:** `POST /api/payments/checkout`, `POST /api/payments/webhook` (system).
**Published events:** `PaymentSucceeded` (carries `BookingId`, `PaymentId`, `Amount`), `PaymentFailed`.
**Consumes:** `BookingConfirmed` → `Payment.CreatePending`; `BookingExpired`/`BookingRejected` → `Cancel` pending payment; `BookingCancelled` → cancel pending payment, or if paid `Deposit.Deduct(PenaltyAmount)` (refund mechanics are a gap, Section 9); `BookingCompleted` → set `ReleaseAfter`; `TicketOpened` → `Freeze`; `TicketResolved` → `SettleByRuling`.

---

### 4.5 Handover — schema `handover`

**Aggregate root `Handover`** (table `Handovers`): `Id`, `BookingId`, `Type` (`Pickup`/`Return`), `ServerReceivedAt` (server clock, BR-13), `Status` (`Draft`/`Confirmed`). Unique `(BookingId, Type)`. **[ASSUMPTION]** `ConfirmedByCustomerAt`, `ConfirmedByOwnerAt`.
**Child entities:**
- `VehicleCondition` (1:1, PK `HandoverId`): `Odo`, `FuelLevel` (0–100), `DamageNote`.
- `HandoverPhoto` (`Id`, `Corner`: `FrontLeft`/`FrontRight`/`BackLeft`/`BackRight`/`Dash`..., `StorageKey`, `FileHash`, `ClientCapturedAt`).
**Local read model [ASSUMPTION]** `HandoverContext` (`BookingId`, `VehicleId`, `CustomerId`, `OwnerId`, `StartTime`, `EndTime`) created from `BookingPaid`, so Handover never calls Booking.

**Behavior**
```csharp
static Result<Handover> Start(HandoverContext ctx, HandoverType type, Guid startedBy, DateTimeOffset serverNow); // Draft
Result AddPhoto(Corner corner, string storageKey, string fileHash, DateTimeOffset clientCapturedAt);            // Draft only
Result SetCondition(int odo, int fuelLevel, string? damageNote);                                               // Draft only
Result Confirm(Guid actorId, DateTimeOffset now);   // each party confirms once; when both did => Confirmed + immutable, raises HandoverConfirmed
```
**Invariants:** only the booking's customer and owner act; data is editable only in `Draft` and **immutable after `Confirmed`**; confirmation requires a photo for each of the 4 corners plus `Dash` **[ASSUMPTION]** and a `VehicleCondition`; `FuelLevel` 0–100; `FileHash` mandatory (tamper evidence, BR-13); `ClientCapturedAt` may be earlier than `ServerReceivedAt` (offline capture, BR-14); `Return` can start only after `Pickup` is `Confirmed`, and its `Odo` ≥ the pickup `Odo`.
**API:** `POST /api/handovers` (start Pickup or Return), `POST /api/handovers/{id}/photos`, `PUT /api/handovers/{id}/confirm`.
**Published events:** `HandoverConfirmed` (carries `BookingId`, `Type`, `Odo`, `FuelLevel`, `ConfirmedAt`; for `Return` also `DistanceKm`).
**Consumes:** `BookingPaid` → create `HandoverContext`; `BookingCancelled` → close context.

---

### 4.6 Users — schema `users`

**Aggregate root `User`** (table `Users`): `Id`, `Email` (unique), `Phone` (unique), `PasswordHash`, `Role` (`Guest`/`Customer`/`Owner`/`Admin`). **[ASSUMPTION]** `Status` (`Active`/`Banned`) because the API has `ban`.
**Child entities:**
- `CustomerProfile` (PK `UserId`): `FullName`, `Address`.
- `OwnerProfile` (PK `UserId`): `TaxCode`, `ApprovalStatus` (`Pending`/`Approved`/`Rejected`). **[ASSUMPTION]** `RejectReason`.
- `IdentityDocument` (`Id`, `Type`: `CCCD`/`GPLX`, `StorageKey` — presigned access only, NFR-05). **[ASSUMPTION]** `VerificationStatus` (`Pending`/`Verified`/`Rejected`).
`Users` is mapped onto the ASP.NET Core Identity user (`PasswordHash` etc.); `Role` stays a single value as in the source.

**Behavior**
```csharp
static Result<User> Register(email, phone, passwordHash);                 // Role = Customer
Result UpdateProfile(fullName, address);
Result AddIdentityDocument(DocumentType type, string storageKey);         // replaces a document of the same type => Pending
Result SubmitOwnerOnboarding(string taxCode, IEnumerable<string> businessDocKeys); // OwnerProfile Pending
Result ApproveOwner(Guid adminId);   Result RejectOwner(Guid adminId, string reason);  // Approved => Role = Owner
Result VerifyDocument(Guid docId, Guid adminId, bool ok, string? reason); // [ASSUMPTION]
Result Ban(Guid adminId, string reason);   Result Unban(Guid adminId);
EligibilityResult GetBookingEligibility();  // Active + FullName present + CCCD and GPLX present (and Verified if verification is used)
```
**Invariants:** unique email/phone; banned users cannot book or list vehicles; only `Approved` owners get `Role = Owner`; document storage keys are never exposed as public URLs.

**Authentication split**

| Concern | Owner |
|---|---|
| Register, login (email/password, Google, Facebook), JWT + refresh token issuing, logout, revocation | **Users** (Identity + endpoints) |
| JWT signature/expiry/issuer/audience validation, role policies, revocation check | **API host** (first line of defense, read-only) |
| Data-level permissions (is this my booking?) | the module owning the data |

**Revocation [ASSUMPTION]:** Redis keys `revoked:jti:{jti}` (TTL = remaining token life) and `user:tokens_valid_after:{userId}` (timestamp; the host rejects tokens with an older `iat`). `ITokenRevocationStore` is defined in a shared place; Users writes, the host reads. `Ban` writes `tokens_valid_after` synchronously. Access tokens live 10–15 minutes. Refresh token entity **[ASSUMPTION]** `RefreshToken` (`Id`, `UserId`, `TokenHash`, `ExpiresAt`, `RevokedAt`, `ReplacedByTokenId`, `DeviceInfo`, `CreatedAt`): random value (not a JWT), stored hashed, rotated on every use, reuse of an old token revokes the chain.

**API:** `POST /api/users/auth/register`, `POST /api/users/auth/login`, `PUT /api/users/me/profile`, `POST /api/users/me/identity-docs` (returns presigned upload URL), `POST /api/users/me/owner-onboarding`, `PUT /api/users/{id}/ban` (Admin).
**Published events:** none required by the source flows. **Contracts:** `IUsersApi.GetBookingEligibilityAsync`, `GetContactAsync`.

---

### 4.7 Fleet — schema `fleet`

**Aggregate root `Vehicle`** (table `Vehicles`): `Id`, `OwnerId` (indexed), `PlateNumber` (unique), `BrandId`, `ModelId`, `Transmission` (`Auto`/`Manual`), `Seats`, `Status` (`Draft`/`PendingApproval`/`Published`/`Hidden`).
**Child entities:**
- `VehicleImage` (`Id`, `StorageKey`, `IsPrimary`).
- `VehicleApproval` (`Id`, `AdminId`, `Decision`: `Approved`/`Rejected`, `Reason`, `CreatedAt`) — decision history.
**[ASSUMPTION]** extra properties: `Location` (`GeoPoint`) and `Address` text (the search document needs a location but `Vehicles` has none), `HiddenByAdmin` flag. **[ASSUMPTION]** reference data `VehicleBrand` / `VehicleModel` (`Id`, `Name`, `BrandId`) because `BrandId`/`ModelId` need a catalog.

**State machine:** `Draft → PendingApproval → Published ⇄ Hidden`. Admin reject returns the vehicle to `Draft` (recorded in `VehicleApproval`); admin unpublish → `Hidden` with `HiddenByAdmin = true` (owner must resubmit); owner hide → `Hidden`, owner may `Show()` it again when not hidden by admin.

**Behavior**
```csharp
static Result<Vehicle> Create(ownerId, plateNumber, brandId, modelId, transmission, seats, GeoPoint location, string address); // Draft
Result UpdateDetails(...);                       // plate immutable once approved; raises VehiclePublished again if Published
Result AddImage(storageKey);  Result RemoveImage(imageId);  Result SetPrimaryImage(imageId);
Result SubmitForApproval();                      // needs a primary image and >= 3 images [ASSUMPTION], location set
Result Approve(Guid adminId);                    // PendingApproval -> Published, adds VehicleApproval, raises VehiclePublished
Result Reject(Guid adminId, string reason);      // PendingApproval -> Draft
Result Unpublish(Guid adminId, string reason);   // Published -> Hidden (admin), raises VehicleUnpublished
Result Hide();   Result Show();                  // owner
```
**Invariants:** exactly one primary image; only `Published` vehicles can be booked/searched; owner must have `Role = Owner` (API policy); `PlateNumber` unique; an owner edits only their own vehicles.
**API:** `POST /api/fleet/vehicles` (starts `PendingApproval` per source; implement as create `Draft` + submit in the same request **[ASSUMPTION]**), `PUT /api/fleet/vehicles/{id}`, `GET /api/fleet/vehicles/{id}`, `PUT /api/fleet/vehicles/{id}/approve` (Admin: approve or unpublish).
**Published events:** `VehiclePublished` (full snapshot: id, owner, brand, model, seats, transmission, location; re-published on updates), `VehicleUnpublished`.
**Contracts:** `IFleetApi.GetVehicleSummaryAsync`.

---

### 4.8 Search — schema `search` (read model)

**Read model `VehicleSearchDocument`** (table `VehicleSearchDocuments`): `VehicleId` (PK), `Location` (PostGIS `Geometry`), `Brand`, `Model`, `Seats`, `Transmission`, `PriceFrom`, `AverageRating`. **[ASSUMPTION]** `IsPublished`, `LastFleetEventAt`, `LastPricingEventAt`, `LastRatingEventAt` so out-of-order events converge (price/rating events may arrive before the publish event; stale events are ignored).
**Behavior:** `ApplyPublished(VehiclePublished)`, `ApplyUnpublished(...)`, `ApplyPrice(PricingUpdated)`, `ApplyRating(RatingSummaryUpdated)` — all idempotent, no public setters.
**Visibility rule [ASSUMPTION]:** returned by queries only if `IsPublished` and `PriceFrom` is not null.
**API:** `GET /api/search/vehicles` — pagination, radius filter via PostGIS (`ST_DWithin`), specs (brand, model, seats, transmission) and price filter. Target < 2s (NFR-01).
**Consumes:** `VehiclePublished`, `VehicleUnpublished`, `PricingUpdated`, `RatingSummaryUpdated`.

---

### 4.9 Reviews — schema `reviews`

**Aggregate root `Review`** (table `Reviews`): `Id`, `BookingId` (unique), `VehicleId` (indexed), `CustomerId`, `Rating` (1–5), `Content`, `Status` (`Published`/`Hidden`).
**Aggregate root `RatingSummary`** (table `RatingSummaries`): `VehicleId` (PK), `AverageRating`, `TotalReviews`.
**[ASSUMPTION]** local read model `ReviewableBooking` (`BookingId`, `VehicleId`, `CustomerId`), created from `BookingCompleted`; it enforces "only completed bookings".

**Behavior**
```csharp
static Result<Review> Create(ReviewableBooking booking, Guid customerId, int rating, string content, now);
Result Hide(Guid adminId, string reason);    Result Restore(Guid adminId);   // moderation ("hide/delete" => Hidden)
// RatingSummary
void AddRating(int rating);   void RemoveRating(int rating);                  // keeps AverageRating/TotalReviews consistent
```
**Invariants:** rating 1–5; one review per booking; only the booking's customer reviews; only `Published` reviews count in the summary. `Create`/`Hide`/`Restore` update `RatingSummary` in the same transaction (in-module domain event handler).
**API:** `POST /api/reviews`, `GET /api/reviews/vehicles/{id}` (published reviews + summary), `PUT /api/reviews/{id}/moderate` (Admin).
**Published events:** `RatingSummaryUpdated` (carries `VehicleId`, `AverageRating`, `TotalReviews`).
**Consumes:** `BookingCompleted`.

---

### 4.10 Support — schema `support`

**Aggregate root `SupportTicket`** (table `SupportTickets`): `Id`, `BookingId`, `ReporterId`, `Status` (`Open`/`InProgress`/`Resolved`). **[ASSUMPTION]** resolution fields: `DepositToCustomerPercent` (0–100), `ResolutionNote`, `ResolvedBy`, `ResolvedAt`.
**Child entity `TicketMessage`** (`Id`, `SenderId`, `Content`, `AttachmentKey` — evidence).

**Behavior**
```csharp
static Result<SupportTicket> Open(bookingId, reporterId, firstMessage, attachmentKey, now);  // reporter must be a booking participant (IBookingApi)
Result AddMessage(Guid senderId, string content, string? attachmentKey, now);
Result StartReview(Guid adminId);                                  // Open -> InProgress
Result Resolve(Guid adminId, int depositToCustomerPercent, string note, now); // final ruling on the deposit split
```
**Invariants:** no new messages after `Resolved`; only an Admin resolves; percent 0–100; one unresolved ticket per `(BookingId, ReporterId)` **[ASSUMPTION]**.
**API:** `POST /api/support/tickets`, `PUT /api/support/tickets/{id}/resolve` (Admin).
**Published events:** `TicketOpened` (so Payments freezes the deposit), `TicketResolved` (carries `BookingId`, `DepositToCustomerPercent`).

---

### 4.11 Notifications — schema `notifications`

**Aggregate root `Notification`** (table `Notifications`): `Id`, `UserId`, `Type` (`Email`/`SMS`/`Push`), `Content` (JSONB payload: template key + data), `Status` (`Pending`/`Sent`/`Failed`). **[ASSUMPTION]** `Attempts`, `LastError`, `CreatedAt`, `SentAt`, `DedupKey` (unique, prevents duplicates when an event is redelivered).
**Behavior:** `Create(userId, type, content, dedupKey, now)`, `MarkSent(now)`, `MarkFailed(error)` (max 3 attempts, then stays `Failed`).
**Consumes (event → recipients):** `BookingRequested` → owner; `BookingConfirmed`, `BookingRejected`, `BookingExpired` → customer; `BookingCancelled` → both; `PaymentSucceeded` → customer and owner (source: Email/SMS); `PaymentFailed` → customer; `TicketResolved` → ticket parties; `VehiclePublished`/`VehicleUnpublished` → owner. Contact data via `IUsersApi.GetContactAsync`.
**Published events:** none.

---

### 4.12 Audit — schema `audit`

**Append-only `AuditLogEntry`** (table `AuditLogEntries`): `Id`, `Timestamp`, `ActorId`, `IpAddress`, `Action` (e.g. `ForceCancelBooking`, `BanUser`), `EntityName`, `EntityId`, `BeforeState` (JSONB), `AfterState` (JSONB).
**Behavior:** only a static factory `Record(...)`; no update/delete methods; repository exposes `Add` and queries; the DB role has no `UPDATE/DELETE`.
**Input [ASSUMPTION]:** Audit subscribes to no business events. Each module's Application pipeline behavior sends a `RecordAuditEntry` message (defined in `Audit.Contracts`) for every command marked `IAuditedCommand` — all Admin actions (ban, vehicle approve/unpublish, admin-cancel, moderate review, resolve ticket) — and for every access to an identity document. Never put full CCCD/GPLX numbers in `BeforeState/AfterState`. (Pricing history for NFR-13 lives in `PricingPlanVersions`, not here.)
**Published events / consumes:** none.

---

### 4.13 Reporting — schema `reporting` (read model)

**Read model `DailyBookingStats`**: `Date` (PK), `TotalBookings`, `TotalRevenue`, `CancellationCount`. Date buckets use UTC+7 **[ASSUMPTION]**.
**Behavior:** `ApplyBookingRequested`, `ApplyBookingCancelled`, `ApplyPaymentSucceeded(amount)`; each returns whether it changed anything. **[ASSUMPTION]** a `processed_events (EventId PK)` table gives idempotency.
**Consumes:** `BookingRequested` → `TotalBookings`; `BookingCancelled` → `CancellationCount`; `PaymentSucceeded` → `TotalRevenue`.
**API:** `GET /api/reporting/dashboard` (Admin). *Note:* the endpoint description also mentions user counts, which `DailyBookingStats` has no column for (Section 9).

---

## 5. Integration Event Catalog

Naming follows the source: `XxxIntegrationEvent`, immutable records with `EventId`, `OccurredAt`, IDs and the minimum data needed (never entities). The three names in **bold** are named in the source; the others are **[ASSUMPTION]**. **16 events in total.**

| Event (`…IntegrationEvent`) | Published by | Consumers |
|---|---|---|
| `BookingRequested` | Booking | Notifications, Reporting |
| `BookingConfirmed` | Booking | Payments, Notifications |
| `BookingPaid` | Booking | Handover |
| `BookingRejected` | Booking | Payments, Notifications |
| `BookingCancelled` | Booking | Payments, Handover, Notifications, Reporting |
| `BookingExpired` | Booking | Payments, Notifications |
| `BookingCompleted` | Booking | Reviews, Payments |
| **`PaymentSucceeded`** | Payments | Booking, Notifications, Reporting |
| `PaymentFailed` | Payments | Notifications |
| `HandoverConfirmed` | Handover | Booking |
| **`VehiclePublished`** | Fleet | Search, Notifications |
| `VehicleUnpublished` | Fleet | Search, Notifications |
| **`PricingUpdated`** | Pricing | Search |
| `RatingSummaryUpdated` | Reviews | Search |
| `TicketOpened` | Support | Payments |
| `TicketResolved` | Support | Payments, Notifications |

Not events: Availability changes (synchronous calls), Audit entries (`RecordAuditEntry` message, see 4.12).

---

## 6. Key Flows (for integration checks)

1. **Create booking:** `POST /api/bookings` → Users eligibility → Fleet summary → Pricing quote → Availability `PlaceHold` (≤ 12h) → `Booking(Pending)` + `BookingRequested`.
2. **Owner approves:** `PUT approve` → `ConfirmHold` (Hold → Booked) → `Booking(Confirmed)` → `BookingConfirmed` → Payments creates `Payment(Pending)`, Notifications informs the customer. Reject/expire/cancel → `ReleaseHold`.
3. **Payment:** `POST /api/payments/checkout` → gateway → webhook (`WebhookInbox` dedupe) → `Payment(Success)` + `Deposit(Held)` → `PaymentSucceeded` → Booking `Paid` + `BookingPaid` → Handover context; Notifications emails/SMS both parties; Reporting adds revenue.
4. **Pickup / Return:** `POST handovers` → photos (hashed) → condition → both parties `confirm` → `HandoverConfirmed` → Booking `InProgress` (Pickup) / `Completed` (Return) → `BookingCompleted` → Reviews opens eligibility, Payments sets `ReleaseAfter`.
5. **Deposit settlement:** no ticket → `ReleaseDueDepositsJob` releases; ticket opened → `Freeze`; admin `resolve` → `TicketResolved` → `SettleByRuling` (Released / Deducted / Forfeited).
6. **Search sync:** `VehiclePublished` / `PricingUpdated` / `RatingSummaryUpdated` → `VehicleSearchDocuments` (eventually consistent).
7. **Cancel:** customer `cancel` → penalty from `CancellationPolicySnapshot` → release calendar → `BookingCancelled` (with `PenaltyAmount`) → Payments cancels pending payment or deducts the penalty from the deposit.

---

## 7. Definition of Done per module

- [ ] Aggregates/entities match Section 4; **no public setters**; every transition goes through a method.
- [ ] Table/column/enum names match the source (plus only the tagged **[ASSUMPTION]** columns).
- [ ] Domain unit tests cover transitions, invariants, and raised events.
- [ ] EF configuration + migration in the module schema; no FK outside the schema; exclusion constraint in Availability.
- [ ] `*.Contracts` exposes only what Section 3/5 lists.
- [ ] Event handlers idempotent (tested twice); outbox integration test with Testcontainers.
- [ ] NetArchTest passes; Admin endpoints use the `Admin` policy and emit audit entries.

---

## 8. Source API coverage (checklist)

Search: `GET /api/search/vehicles`. Fleet: `GET|POST /api/fleet/vehicles`, `PUT /api/fleet/vehicles/{id}`, `PUT .../approve`. Reviews: `GET /api/reviews/vehicles/{id}`, `POST /api/reviews`, `PUT /api/reviews/{id}/moderate`. Users: `register`, `login`, `PUT me/profile`, `POST me/identity-docs`, `POST me/owner-onboarding`, `PUT {id}/ban`. Availability: `GET .../check`, `POST .../blocks`. Pricing: `POST /api/pricing/quotes`, `PUT /api/pricing/vehicles/{id}`. Booking: `POST /api/bookings`, `GET me`, `GET owner`, `PUT cancel|approve|reject|admin-cancel`. Payments: `POST checkout`, `POST webhook`. Handover: `POST /api/handovers`, `POST .../photos`, `PUT .../confirm`. Support: `POST /api/support/tickets`, `PUT .../resolve`. Reporting: `GET /api/reporting/dashboard`.

---

## 9. Gaps in the source that this design had to fill (team to confirm)

1. **Deposit vs. payment:** `Payments.Amount` is "total paid" while `Deposits` is separate. Assumed: one payment covers Rent + Deposit; on success a `Held` deposit is created. **Refunds** of rent after cancellation are not modeled in the source (no refund table/status).
2. **Payment deadline:** a `Booked` calendar entry never expires, so a payment deadline (default 2h after approval) was added; otherwise an unpaid confirmed booking blocks the vehicle forever.
3. **Who moves the booking to `InProgress`/`Completed`:** assumed `HandoverConfirmed` (Pickup/Return).
4. **Missing admin endpoints:** approve/reject owner onboarding, verify identity documents, confirm manual `Transfer` payments. Add them in Users/Payments if needed.
5. **Single `Role` enum:** an `Owner` cannot also rent as `Customer`. Keep, or change to multi-role.
6. **Cancellation policy origin:** the source only stores a snapshot; assumed a platform-level default policy provided by Pricing configuration.
7. **Fleet location:** `Vehicles` has no location column but Search needs one; `Location` and `Address` were added.
8. **Dashboard user count:** mentioned by `GET /api/reporting/dashboard` but absent from `DailyBookingStats`.
9. **Over-km fee settlement:** `OverKmFee` exists as a line-item type but nothing defines when/how it is charged; assumed it is only recorded on completion.
10. **Extra synchronous reads** (Fleet summary, Booking participants, Users contact) and `CarRental.BuildingBlocks` references.
11. **Vehicle brand/model catalog** (`BrandId`, `ModelId` need reference data).
12. **Manual `Transfer` flow:** the architecture document mentions a manual receipt flow, but no table/status exists for receipts.

## 10. Optional extensions [EXT] — do not implement by default

Weekly/monthly discounts, seasonal price rules, delivery fee, `Promotion`/voucher aggregate, persisted `Quote`, owner payout aggregate, bidirectional reviews / owner replies / review windows, a separate `Dispute` aggregate, multi-channel notification preferences and in-app notifications, richer Reporting projections (per-owner earnings, per-vehicle performance), `UserRegistered` event for user statistics, availability filter inside Search.