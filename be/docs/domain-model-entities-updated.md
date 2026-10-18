# Domain Model Design (Rich Domain Model) for All Modules

> **Audience:** the sub-agents implementing the code. Each module can be assigned to an independent agent. Every agent must read **Section 1 (Common Conventions)** and **Section 2 (Shared Kernel)** first, then only its own module section plus the **Contracts** of the modules it depends on.
>
> **Source:** `System Design, Database and API` (Modular Monolith, .NET 9, ASP.NET Core, PostgreSQL + PostGIS, one EF Core DbContext per module, Wolverine outbox).
>
> **Notation:** `[ASSUMPTION]` marks details the source document does not specify. Agents should implement them as written but keep the values in configuration/constants so they are easy to change.

---

## 1. Common Conventions (mandatory for every module)

### 1.1 Rich Domain Model principles

1. **No anemic entities.** Every state change must go through a business method on the Aggregate Root (`Approve()`, `Cancel()`, `PlaceHold()`, ...). `public set;` is forbidden.
2. **Invariants live in the domain**, not in Application Services or Controllers/Endpoints. The Application layer only does: load aggregate → call method → save → dispatch.
3. **Private constructor + static factory method** `Create(...)` returning `Result<T>` (or throwing `DomainException` for programmer-level invariant violations). Add a `private` parameterless constructor for EF Core.
4. **Collections** inside an aggregate: a `private readonly List<T> _items` field exposed as `IReadOnlyCollection<T> Items`.
5. **Domain events:** the aggregate calls `Raise(new XxxDomainEvent(...))`. Events are converted to integration events through the **Wolverine outbox** in the same transaction as `SaveChanges`.
   **Event policy (keep the contract surface small):** an event is **published** as an integration event only if at least one other module needs to react to it. Otherwise it stays an **internal** domain event (handled inside the module or not handled at all). Section 5 is the single source of truth for published events; each module section lists them as *Published events*. Do not add a published event without adding a consumer in Section 5. Similar events are merged into one event carrying the new status (e.g. `VehicleStatusChanged`).
6. **Do not call `DateTime.UtcNow` / `Guid.NewGuid()` inside the domain** for testable logic: pass `DateTimeOffset now` into methods (taken from `TimeProvider` in the Application layer). IDs use `Guid.CreateVersion7()` in the factory.
7. **Strongly-typed IDs** for aggregate roots (`readonly record struct BookingId(Guid Value)`) with EF value converters.
8. **Value Objects** are immutable `record`s/classes that self-validate in their factory and are mapped with `OwnsOne`/`ComplexProperty`.
9. **Enums** are stored as **strings** in the DB (`HasConversion<string>()`).
10. **One aggregate = one transaction.** Reference other aggregates by ID only (`VehicleId`, `UserId`, ...).

### 1.2 Modular monolith constraints

- Each module has its own PostgreSQL schema: `users`, `fleet`, `availability`, `pricing`, `booking`, `payments`, `handover`, `search`, `reviews`, `support`, `notifications`, `audit`, `reporting`.
- **No** foreign keys or joins across schemas. A module only stores the IDs of other modules (`Guid`/strongly-typed IDs defined in Contracts or the Shared Kernel).
- Module A may only reference module B's `*.Contracts` project (synchronous interfaces, DTOs, integration events). NetArchTest will fail the build on violations.
- **Synchronous** calls only for: Booking → Pricing (quote), Booking → Availability (hold), Booking → Users (verified profile). Everything else uses **integration events through the outbox**; handlers must be **idempotent**.
- Admin is **not a module**: admin endpoints live inside the module that owns the business capability and are protected by the `Admin` policy.

### 1.3 Suggested project structure for each module `X`

```
src/Modules/X/
  X.Contracts/        # synchronous interfaces, DTOs, integration events (public)
  X.Domain/           # aggregates, entities, value objects, domain events, enums, domain services (no EF/ASP.NET dependency)
  X.Application/      # command/query handlers, domain event -> integration event mapping
  X.Infrastructure/   # XDbContext, EF configuration, repositories, migrations, gateway adapters
  X.Api/              # the module's endpoint group (Minimal API)
tests/Modules/X/      # X.Domain.Tests (pure unit tests), X.Integration.Tests (Testcontainers)
```

### 1.4 EF Core / PostgreSQL conventions

- One `DbContext` per module, `HasDefaultSchema("<schema>")`. Configure with `IEntityTypeConfiguration<T>` in `Infrastructure`.
- `snake_case` column names. Time is `timestamptz` (UTC). Money: see `Money` below.
- Optimistic concurrency: each aggregate root has `uint Version` mapped to the `xmin` system column (`IsRowVersion()`), or an auto-incremented `version` column.
- Double-booking prevention: an **exclusion constraint** (`btree_gist` + `tstzrange`) on the Availability block table, in addition to the check inside the domain.
- Geography: `geography(Point,4326)` (PostGIS) with a GIST index.
- Soft delete only where explicitly stated; by default use statuses (`Archived`, `Cancelled`).

### 1.5 Testing conventions

- Every aggregate must have pure unit tests (no DB) covering: every valid transition, every forbidden transition, every invariant, and that the domain event is raised.
- Test naming: `Method_Condition_ExpectedResult`.

---

## 2. Shared Kernel (write first, shared by all)

> The `Shared.Kernel` project contains only building blocks and **no business logic**. [ASSUMPTION] modules may reference this project (in addition to `Contracts`); update the NetArchTest rules accordingly.

```csharp
public abstract class Entity<TId> where TId : notnull
{
    public TId Id { get; protected set; } = default!;
    // Equals/GetHashCode by Id and type
}

public interface IDomainEvent { Guid EventId { get; } DateTimeOffset OccurredAt { get; } }

public abstract class AggregateRoot<TId> : Entity<TId> where TId : notnull
{
    private readonly List<IDomainEvent> _events = new();
    public IReadOnlyCollection<IDomainEvent> DomainEvents => _events;
    public uint Version { get; private set; }              // xmin
    protected void Raise(IDomainEvent e) => _events.Add(e);
    public void ClearDomainEvents() => _events.Clear();
}

public abstract record ValueObject;                          // value-based equality

public sealed record Error(string Code, string Message);     // e.g. "booking.invalid_transition"
public readonly record struct Result { /* IsSuccess, Error */ }
public readonly record struct Result<T> { /* IsSuccess, Value, Error */ }
public class DomainException(string code, string message) : Exception(message) { public string Code { get; } = code; }

public static class Guard { /* AgainstNull, AgainstNullOrWhiteSpace, AgainstOutOfRange, Against(bool, code, msg) */ }
```

### Shared Value Objects

| Value Object | Properties | Invariants / behavior |
|---|---|---|
| `Money` | `long Amount` (smallest unit; VND has no fractional part), `string Currency = "VND"` | `Amount >= 0`; `Add`, `Subtract` (never negative), `Multiply(decimal)` rounded to VND, `Percent(decimal)`; adding different `Currency` values is forbidden. EF: `OwnsOne` → columns `*_amount`, `*_currency`. |
| `DateTimeRange` | `DateTimeOffset Start`, `End` | `End > Start`; `Overlaps(other)`, `Contains(instant)`, `Duration`, `TotalDays` (rounded up to whole days `[ASSUMPTION]` 24h/day). Used for rental periods (UTC). |
| `GeoPoint` | `double Latitude`, `Longitude` | lat ∈ [-90,90], lon ∈ [-180,180]; mapped to `geography(Point)`. |
| `Address` | `Line`, `Ward`, `District`, `City`, `Country = "VN"`, `GeoPoint? Location` | `Line` and `City` are required. |
| `PhoneNumber` | `string E164` | Validates E.164 and normalizes VN numbers (`0xxx` → `+84xxx`). |
| `Rating` | `byte Value` | 1..5. |
| `FileReference` | `string StorageKey`, `string? Url`, `bool IsPrivate`, `string ContentType`, `long SizeBytes` | Vehicle photos → Cloudinary (public). Identity documents → `IsPrivate = true`, **never store a public URL**; issue a signed URL only when needed. |

---

## 3. Module Map and Dependencies

| Group | Module | Main Aggregate Roots | Synchronous dependencies (Contracts) |
|---|---|---|---|
| Rental flow | **Booking** | `Booking` | `IPricingApi`, `IAvailabilityApi`, `IUsersApi` |
| | **Pricing** | `PricingPlan`, `Promotion` | — |
| | **Availability** | `VehicleCalendar` | — |
| | **Payments** | `Payment`, `OwnerPayout` | — (payment gateway hidden behind the module's own interface) |
| | **Handover** | `Handover` | — |
| Catalog and people | **Users** | `UserProfile` | — |
| | **Fleet** | `Vehicle` | — |
| | **Search** | `SearchableVehicle`, `Favorite` | `IAvailabilityApi` (calendar filtering) |
| | **Reviews** | `ReviewWindow`, `Review` | — |
| Support and platform | **Support** | `SupportTicket`, `Dispute` | — |
| | **Notifications** | `Notification`, `NotificationPreference` | — |
| | **Audit** | `AuditEntry` | — |
| | **Reporting** | read models (projections) | — |

> [Trade-off already decided in the source document] In the early phase, Pricing + Availability may be folded into Fleet, and Notifications + Audit + Reporting into a single `Platform` module, **but keep the namespaces/schemas/aggregates separate exactly as designed below** so that extracting a module later requires no rewrite.

---

## 4. Detailed Design per Module

### 4.1 Booking (core domain)

**Aggregate Root: `Booking`**

| Property | Type | Notes |
|---|---|---|
| `Id` | `BookingId` | |
| `Code` | `string` | human-readable code (e.g. `BK-20261017-X7K2`), unique |
| `RenterId`, `OwnerId` | `Guid` | Users IDs (owner is obtained from Fleet via Contracts at creation) |
| `VehicleId` | `Guid` | Fleet ID |
| `Period` | `DateTimeRange` | rental period |
| `PickupAddress`, `ReturnAddress` | `Address` | |
| `Pricing` | `PricingSnapshot` (VO) | snapshot of the quote: `QuoteId`, `Lines[]`, `Total`, `DepositAmount`, `PlatformFee`, `OwnerEarning`, `PromotionCode?` — **immutable after creation** |
| `CancellationPolicy` | `CancellationPolicy` (VO) | refund tiers by cancellation time |
| `Status` | `BookingStatus` | see state machine |
| `OwnerResponseDeadline` | `DateTimeOffset` | `RequestedAt + 12h` (per source document) |
| `PaymentDeadline` | `DateTimeOffset?` | `[ASSUMPTION]` `ApprovedAt + 2h` |
| `HoldId` | `Guid?` | hold block ID in Availability |
| `Cancellation` | `CancellationInfo?` (VO) | `By` (Renter/Owner/System/Admin), `Reason`, `At`, `RefundPercent` |
| `RejectionReason` | `string?` | |
| `History` | `List<BookingStatusChange>` (child entity) | `From`, `To`, `At`, `ActorId`, `Note` — append-only |
| `RequestedAt`, `ApprovedAt`, `ConfirmedAt`, `StartedAt`, `CompletedAt` | `DateTimeOffset?` | |

**State machine** (order follows the use case: owner approves → renter pays; if the team reverses the order, only this class changes):

```
Requested ──Approve──► AwaitingPayment ──MarkPaid──► Confirmed ──StartTrip──► InProgress ──CompleteTrip──► Completed
   │                        │                            │
   ├─Reject──► Rejected     ├─ExpirePayment──► Expired   └─Cancel──► Cancelled
   ├─ExpireRequest► Expired └─Cancel──► Cancelled
   └─Cancel (renter)──► Cancelled
```

Any transition outside the diagram → `Result.Failure("booking.invalid_transition")`.

**Behavior (public methods):**

```csharp
static Result<Booking> Request(RenterId, OwnerId, VehicleId, DateTimeRange period, Address pickup, Address ret,
                               PricingSnapshot pricing, Guid holdId, CancellationPolicy policy, DateTimeOffset now);
Result Approve(Guid ownerId, DateTimeOffset now);                 // owner only, before OwnerResponseDeadline
Result Reject(Guid ownerId, string reason, DateTimeOffset now);
Result MarkPaid(Guid paymentId, Money paid, DateTimeOffset now);  // paid >= DepositAmount; driven by PaymentSucceeded event
Result Cancel(CancelledBy by, Guid actorId, string reason, DateTimeOffset now); // computes RefundPercent from policy + timing
Result Expire(DateTimeOffset now);                                // overdue Requested or AwaitingPayment
Result StartTrip(DateTimeOffset now);                             // from PickupCompleted event, only when Confirmed
Result CompleteTrip(DateTimeOffset now);                          // from ReturnCompleted event, only when InProgress
```

**Invariants:**
- `Period.Start` must be in the future at `Request`; minimum duration `[ASSUMPTION]` 4 hours, maximum 30 days.
- `RenterId != OwnerId`.
- `Approve/Reject` only by `OwnerId`; `Cancel` only by `RenterId`, `OwnerId`, or Admin/System.
- `Pricing` never changes after creation.
- `Completed` / `Cancelled` / `Rejected` / `Expired` are terminal states.

**Published events (8):** `BookingRequested`, `BookingApproved`, `BookingRejected`, `BookingConfirmed`, `BookingCancelled` (with `RefundPercent`), `BookingExpired`, `TripStarted`, `TripCompleted`.

**Consumes:** `PaymentSucceeded` → `MarkPaid`; `PickupCompleted` → `StartTrip`; `ReturnCompleted` → `CompleteTrip`.
**Domain/application service:** `BookingPlacementService` (Application): creation flow = `IUsersApi.GetRenterEligibility` → `IPricingApi.GetQuote` → `IAvailabilityApi.PlaceHold` → `Booking.Request`; if anything fails after the hold is placed, release the hold (compensation).
**Hold handling:** Booking owns every deadline. After `Approve`, the Application layer calls `IAvailabilityApi.ExtendHoldAsync` to move the hold to `PaymentDeadline`. On `Reject/Cancel/Expire` the Application layer calls `ReleaseHoldAsync` (Availability also has a safety-net cleanup job).
**Background job:** `ExpireOverdueBookingsJob` scans overdue `Requested/AwaitingPayment` bookings → `Expire`.

---

### 4.2 Pricing

**Aggregate Root: `PricingPlan`** (one plan per vehicle)

| Property | Type | Notes |
|---|---|---|
| `VehicleId` | `Guid` | unique |
| `BaseDailyRate` | `Money` | > 0 |
| `WeekendSurchargePercent` | `decimal` | 0..100 |
| `WeeklyDiscountPercent`, `MonthlyDiscountPercent` | `decimal` | applied for ≥ 7 / ≥ 30 days |
| `SecurityDeposit` | `Money` | deposit amount |
| `CleaningFee`, `DeliveryFeePerKm` | `Money` | optional |
| `DailyKmLimit`, `ExtraKmFee` | `int?`, `Money?` | over-mileage fee |
| `SeasonalRules` | `List<SeasonalPriceRule>` | child entity: `DateTimeRange`, `Multiplier` (0.5..3) or `FixedDailyRate`; **must not overlap** |
| `CancellationPolicy` | `CancellationPolicy` (VO) | list of tiers `(hoursBeforeStart, refundPercent)` in descending order; valid only if monotonic |

**Behavior:** `Create`, `ChangeBaseRate`, `SetDiscounts`, `AddSeasonalRule` (reject overlaps), `RemoveSeasonalRule`, `SetCancellationPolicy`, and **`CalculateQuote(DateTimeRange period, Promotion? promo, DateTimeOffset now, PlatformFeePolicy fee) : Result<Quote>`** (pure, no side effects).

**Entity/VO: `Quote`** (persisted, expires after `[ASSUMPTION]` 15 minutes): `Id`, `VehicleId`, `RenterId`, `Period`, `Lines: List<QuoteLine>` (`Type`: BaseRate/WeekendSurcharge/Discount/Cleaning/Promotion, `Description`, `Amount`), `Subtotal`, `Total`, `Deposit`, `PlatformFee`, `OwnerEarning`, `ExpiresAt`. `IsValid(now)`; `Consume(now)` marks it used (a quote can be used only once).

**Aggregate Root: `Promotion`**: `Code` (unique, upper-case), `Type` (Percent/FixedAmount), `Value`, `MaxDiscount?`, `MinBookingValue?`, `ValidPeriod`, `TotalUsageLimit?`, `PerUserLimit`, `Redemptions: List<PromotionRedemption>` (`UserId`, `BookingId`, `At`), `IsActive`. Behavior: `Validate(userId, bookingValue, now)`, `Redeem(userId, bookingId, now)` (idempotent per `BookingId`), `Deactivate`.

**Published events (1):** `PricingPlanChanged` (Search needs it to update displayed prices). **Internal only:** `PromotionRedeemed`.
**Consumes:** `BookingConfirmed` → `Promotion.Redeem`; `BookingCancelled/Expired` → release the usage if needed.
**Synchronous contract:** `IPricingApi.GetQuoteAsync(vehicleId, renterId, period, promoCode)`.

---

### 4.3 Availability

**Aggregate Root: `VehicleCalendar`** (one calendar per vehicle; load only the blocks overlapping the time range being processed, never the whole history)

**Child entity: `AvailabilityBlock`**: `Id`, `Range: DateTimeRange`, `Type` (`Hold`, `Booked`, `OwnerBlocked`, `Maintenance`), `BookingId?`, `HoldExpiresAt?` (only when `Hold`), `Reason?`, `CreatedAt`, `ReleasedAt?`.

**Invariants:**
- Two active blocks (`ReleasedAt == null` and not expired) **must not overlap**, including a `[ASSUMPTION]` 2-hour buffer between trips (configurable). The final guarantee is a PostgreSQL **exclusion constraint** (`vehicle_id WITH =, tstzrange(start,end) WITH &&` WHERE active).
- A `Hold` always has `HoldExpiresAt`, at most **12 hours** from creation (per source document). The only exception is a single `ExtendHold` after owner approval, up to the booking's `PaymentDeadline`.
- Only a `Hold` block can be `Confirm`ed; `Booked` never expires on its own.

**Behavior:**
```csharp
Result<Guid> PlaceHold(Guid bookingId, DateTimeRange range, TimeSpan ttl, DateTimeOffset now);
Result ConfirmHold(Guid holdId, DateTimeOffset now);              // Hold -> Booked (clears HoldExpiresAt)
Result ExtendHold(Guid holdId, DateTimeOffset newExpiresAt, DateTimeOffset now); // once, when the owner approves
Result Release(Guid bookingId, DateTimeOffset now);               // all blocks of the booking
Result<Guid> BlockByOwner(DateTimeRange range, string reason, DateTimeOffset now);
Result Unblock(Guid blockId, DateTimeOffset now);                 // only OwnerBlocked/Maintenance
int ExpireHolds(DateTimeOffset now);                              // returns the number of expired holds
bool IsAvailable(DateTimeRange range, DateTimeOffset now);
```

**Published events (0):** none. `HoldPlaced`, `HoldExpired`, `HoldReleased`, `SlotBooked`, `VehicleBlocked` stay **internal** domain events (no other module needs them; Booking enforces its own deadlines).
**Consumes:** `BookingConfirmed` → `ConfirmHold`; `BookingRejected/Cancelled/Expired` → `Release` (idempotent, in case the synchronous release failed); `VehicleStatusChanged` (status `Archived`) → release all future blocks.
**Background job:** `ExpireHoldsJob` runs every minute (safety-net cleanup only).
**Synchronous contract:** `IAvailabilityApi.PlaceHoldAsync`, `ExtendHoldAsync`, `ReleaseHoldAsync`, `AreAvailableAsync(vehicleIds[], range)` (bulk, used by Search).
> Documented risk: a 12-hour hold can occupy a vehicle without payment. Keep the TTL configurable so it can be shortened.

---

### 4.4 Payments

**Aggregate Root: `Payment`**

| Property | Type | Notes |
|---|---|---|
| `BookingId`, `PayerId` | `Guid` | |
| `Purpose` | `PaymentPurpose` | `BookingDeposit`, `RentalBalance`, `DamageCharge` `[ASSUMPTION]` |
| `Amount` | `Money` | > 0 |
| `Method` | `PaymentMethod` | `Gateway`, `ManualReceipt` |
| `Status` | `PaymentStatus` | `Pending` → `Succeeded` / `Failed` / `Expired` / `Cancelled`; `Succeeded` → `PartiallyRefunded` → `Refunded` |
| `IdempotencyKey` | `string` | unique |
| `Attempts` | `List<PaymentAttempt>` | `Provider`, `ProviderTxnId` (unique per provider), `Status`, `RedirectUrl?`, `FailureCode?`, `CreatedAt`, `CompletedAt?` |
| `Receipt` | `ManualReceipt?` | `FileReference`, `Status` (Submitted/Approved/Rejected), `ReviewedBy?`, `RejectReason?` |
| `Refunds` | `List<Refund>` | `Id`, `Amount`, `Reason`, `Status` (Requested/Succeeded/Failed), `ProviderRefundId?` |
| `ExpiresAt` | `DateTimeOffset` | equals the booking's `PaymentDeadline` |

**Behavior:** `Create`, `StartGatewayAttempt(provider, now)`, `RecordGatewayResult(providerTxnId, success, rawCode, now)` (**idempotent**: a repeated webhook must not raise duplicate events), `SubmitReceipt(file, now)`, `ApproveReceipt(adminId, now)`, `RejectReceipt(adminId, reason, now)`, `Expire(now)`, `Cancel`, `RequestRefund(amount, reason, now)` (total refunds ≤ amount collected), `RecordRefundResult(refundId, success, providerRefundId)`.

**Aggregate Root: `OwnerPayout`**: `BookingId` (unique), `OwnerId`, `GrossAmount`, `PlatformFee`, `NetAmount`, `Status` (`Scheduled`, `Paid`, `Failed`, `OnHold`), `ScheduledFor`, `PaidAt?`, `BankAccountSnapshot` (VO: bank, account number, holder name). Behavior: `Schedule`, `Hold` (when a Dispute exists), `Release`, `MarkPaid`, `MarkFailed`.

**Payment gateway:** define `IPaymentGateway` (in `Payments.Domain`/`Application`) with a sandbox adapter in `Infrastructure`; the domain knows nothing about any specific SDK.
**Published events (5):** `PaymentSucceeded`, `PaymentFailed`, `RefundIssued`, `PayoutScheduled`, `PayoutPaid`. **Internal only:** `PaymentExpired` (Booking enforces `PaymentDeadline` itself).
**Consumes:** `BookingApproved` → create `Payment(Pending)`; `BookingExpired` → cancel the pending `Payment`; `BookingCancelled` → cancel the pending `Payment` or `RequestRefund` per `RefundPercent`; `TripCompleted` → `OwnerPayout.Schedule`; `DisputeOpened` → `Hold` the payout; `DisputeResolved` → refund/charge/release.

---

### 4.5 Handover

**Aggregate Root: `Handover`** (one record per booking, created on `BookingConfirmed`)

| Property | Type | Notes |
|---|---|---|
| `BookingId` (unique), `VehicleId`, `RenterId`, `OwnerId` | `Guid` | |
| `Status` | `HandoverStatus` | `Scheduled` → `PickupRecorded` → `PickupCompleted` → `ReturnRecorded` → `ReturnCompleted` |
| `ScheduledPeriod` | `DateTimeRange` | |
| `PickupInspection`, `ReturnInspection` | `Inspection?` | child entity |
| `Summary` | `TripSummary?` | computed at return: `DistanceKm`, `FuelDelta`, `LateMinutes`, `NewDamages[]` |

**Entity `Inspection`**: `Type` (Pickup/Return), `RecordedBy`, `RecordedAt`, `OdometerKm`, `FuelLevelPercent` (0..100), `Notes`, `Photos: List<InspectionPhoto>` (`FileReference`, `Area`: Front/Rear/Left/Right/Interior/Dashboard), `Damages: List<DamageItem>` (`Area`, `Description`, `Severity`, `PhotoRefs[]`), `OwnerConfirmedAt?`, `RenterConfirmedAt?`.

**Invariants:**
- Pickup record: at least `[ASSUMPTION]` 6 photos (all four sides + interior + dashboard).
- Completes only when **both parties confirm** (`PickupCompleted`/`ReturnCompleted`).
- `Return.OdometerKm >= Pickup.OdometerKm`.
- A return cannot be recorded before the pickup is completed.

**Behavior:** `Create(booking info)`, `RecordPickup(inspection)`, `ConfirmPickup(by: Owner|Renter, now)`, `RecordReturn(inspection, now)`, `ConfirmReturn(by, now)`, `ReportDamage(damage)`, plus an internal function that computes `TripSummary` by comparing the two records (`LateMinutes = max(0, ReturnAt − Period.End)`).
**Published events (2):** `PickupCompleted`, `ReturnCompleted` (carries `TripSummary`, including `NewDamages[]`). **Internal only:** `DamageReported`.
**Consumes:** `BookingConfirmed` → create the Handover; `BookingCancelled` → close it.

---

### 4.6 Users

> Authentication/passwords/Google & Facebook login are handled by **ASP.NET Core Identity** in `Infrastructure` (`AspNetUsers` tables, etc.). The domain only has `UserProfile` (same `Id` as the identity user).

**Aggregate Root: `UserProfile`**

| Property | Type | Notes |
|---|---|---|
| `Id` | `UserId` | = Identity user id |
| `Email`, `FullName`, `PhoneNumber?`, `DateOfBirth?`, `Avatar?` | | `Avatar` is a public `FileReference` |
| `Roles` | `HashSet<UserRole>` | `Renter` (default), `Owner` (Admin is managed by Identity) |
| `Status` | `UserStatus` | `Active`, `Suspended`, `Banned` |
| `IdentityDocument` | `IdentityDocument?` (child entity) | `CitizenIdNumber` (CCCD, encrypted/partially masked), `FrontImage`, `BackImage`, `SelfieImage?` (all **private** `FileReference`s), `Status` (`NotSubmitted`, `Pending`, `Verified`, `Rejected`), `ReviewedBy?`, `ReviewedAt?`, `RejectReason?` |
| `DriverLicense` | `DriverLicense?` (child entity) | `Number`, `Class` (A1..E, FC), `ExpiresOn`, `Image` (private), `Status` same as above |
| `PayoutAccount` | `PayoutAccount?` (VO) | bank, account number, holder name (for Owners) |

**Behavior:** `Register`, `UpdateProfile`, `SubmitIdentityDocument`, `ApproveIdentity(adminId)`, `RejectIdentity(adminId, reason)`, `SubmitDriverLicense`, `ApproveDriverLicense`, `RejectDriverLicense`, `BecomeOwner(payoutAccount)` (requires identity `Verified`), `SetPayoutAccount`, `Suspend(reason, adminId)`, `Reinstate`, and the business query **`GetRenterEligibility(now) : EligibilityResult`** (identity `Verified` + license `Verified` and not expired + `Status == Active` + age ≥ 21 `[ASSUMPTION]`; returns the list of reasons when not eligible).

**Invariants:** a `Verified` document that is re-submitted returns to `Pending`; an expired driver license makes the user ineligible; `Suspended/Banned` users cannot create bookings or list vehicles.
**Published events (4):** `UserRegistered`, `IdentityReviewed` (`Approved|Rejected`, with reason), `DriverLicenseReviewed` (`Approved|Rejected`, with reason), `UserSuspended`. **Internal only:** `UserBecameOwner`, `UserReinstated`.
**Synchronous contract:** `IUsersApi.GetRenterEligibilityAsync(userId)`, `GetUserSummaryAsync(userId)`, `IsVerifiedOwnerAsync(userId)`.
**Security:** CCCD/driver-license numbers and images are accessible only through short-lived signed URLs; every access must be written to Audit.

---

### 4.7 Fleet

**Aggregate Root: `Vehicle`**

| Property | Type | Notes |
|---|---|---|
| `OwnerId` | `Guid` | Users |
| `Brand`, `Model`, `Year` | | `Year` ≤ current year, ≥ 1990 `[ASSUMPTION]` |
| `LicensePlate` | `LicensePlate` (VO) | normalized, **unique** |
| `Seats`, `Transmission` (`Manual`/`Automatic`), `FuelType` (`Petrol`/`Diesel`/`Hybrid`/`Electric`) | | |
| `Title`, `Description` | `string` | |
| `Address` | `Address` | `GeoPoint` required on submit |
| `Features` | `HashSet<VehicleFeature>` | A/C, GPS, dashcam, bluetooth, ... |
| `Rules` | `VehicleRules` (VO) | `MinRenterAge`, `MinLicenseYears`, `AllowDelivery`, `FuelPolicy`, `Notes` |
| `Photos` | `List<VehiclePhoto>` | `Id`, `FileReference` (Cloudinary, public), `SortOrder`, `IsPrimary` |
| `Documents` | `List<VehicleDocument>` | `Type` (Registration/Insurance/Inspection), `FileReference` (**private**), `ExpiresOn?`, `Status` |
| `Status` | `VehicleStatus` | `Draft`, `PendingApproval`, `Active`, `Paused`, `Rejected`, `Archived` |
| `RejectionReason?`, `ApprovedBy?`, `ApprovedAt?` | | |

**State machine:** `Draft → PendingApproval → Active ⇄ Paused`; `PendingApproval → Rejected → (edit) → PendingApproval`; `* → Archived` (terminal).

**Behavior:** `Create(owner, ...)` (owner must be `IsVerifiedOwner` — checked in Application, the domain receives the flag), `UpdateDetails`, `Relocate(address)`, `AddPhoto`, `RemovePhoto`, `SetPrimaryPhoto`, `ReorderPhotos`, `AddDocument`, **`Submit()`** (requires: ≥ 5 photos `[ASSUMPTION]`, exactly 1 primary photo, a valid Registration and a non-expired Insurance, a `GeoPoint`), `Approve(adminId)`, `Reject(adminId, reason)`, `Pause()`, `Resume()` (checks documents are still valid), `Archive()` (blocked while active bookings exist — Application checks through Contracts/flag).
**Invariants:** only `Active` vehicles are searchable; `LicensePlate` cannot be edited after `Approved` (Admin only); an `Archived` vehicle cannot be restored.
**Published events (4):** `VehicleApproved`, `VehicleRejected`, `VehicleListingChanged` (any change affecting display/search), `VehicleStatusChanged` (new status: `Paused`, `Active` after resume, or `Archived`). **Internal only:** `VehicleSubmitted`.
**Synchronous contract:** `IFleetApi.GetVehicleSummaryAsync(vehicleId)` (OwnerId, Status, title, primary photo).

---

### 4.8 Search (read model + favorites)

> This module is a **projection** with no complex business logic, but it should still encapsulate state with `Apply*` methods instead of public setters.

**Aggregate/Read model: `SearchableVehicle`**: `VehicleId` (PK), `OwnerId`, `Title`, `Brand`, `Model`, `Year`, `Seats`, `Transmission`, `FuelType`, `Features[]`, `Location: geography(Point)`, `City`, `District`, `PrimaryPhotoUrl`, `BaseDailyRate`, `AverageRating`, `ReviewCount`, `TripCount`, `IsListed`, `SourceVersion` (tracked per event type to skip stale/duplicate events).
**Methods:** `ApplyListing(VehicleListingChanged/Approved)`, `ApplyPricing(PricingPlanChanged)`, `ApplyRating(ReviewPublished...)`, `ApplyTripCompleted`, `Delist(...)` (when Paused/Archived).
**Query:** filter by location (`ST_DWithin`), price range, brand, seats, transmission, fuel type, features; sort by distance/price/rating. After obtaining candidates, call `IAvailabilityApi.AreAvailableAsync` to drop busy vehicles (Search does not store calendars).
**Aggregate: `Favorite`**: `UserId`, `VehicleId` (unique pair), `AddedAt`. Behavior: `Add`, `Remove`.
**Listing rule:** a vehicle is `IsListed` only when it is `Active` **and** a pricing plan exists (the owner creates the plan through Pricing; Search learns about it from `PricingPlanChanged`).
**Consumes:** `VehicleApproved`, `VehicleListingChanged`, `VehicleStatusChanged`, `PricingPlanChanged`, `ReviewPublished`, `ReviewHidden`, `TripCompleted`.

---

### 4.9 Reviews

**Aggregate Root: `ReviewWindow`** (the right to review; created on `TripCompleted`)
`BookingId` (unique), `VehicleId`, `RenterId`, `OwnerId`, `OpensAt`, `ExpiresAt` (`OpensAt + 14 days` `[ASSUMPTION]`), `RenterReviewed`, `OwnerReviewed`. Behavior: `CanReview(by, now)`, `MarkReviewed(by)`.

**Aggregate Root: `Review`**: `Id`, `BookingId`, `VehicleId`, `ReviewerId`, `RevieweeId`, `Direction` (`RenterToOwnerAndVehicle`, `OwnerToRenter`), `Rating` (VO 1..5), `SubRatings` (VO: cleanliness, accuracy, communication — optional), `Comment` (≤ 2000 characters), `Status` (`Published`, `Hidden`), `OwnerReply?` (VO: text, at), `CreatedAt`, `EditedAt?`.
**Invariants:** exactly one review per `(BookingId, Direction)`; can only be created when `ReviewWindow.CanReview`; editable within 24 hours `[ASSUMPTION]`; only the vehicle owner may `Reply` (once).
**Behavior:** `Create(window, ...)`, `Edit`, `Reply(ownerId, text, now)`, `Hide(adminId, reason)`, `Unhide`.
**Published events (2):** `ReviewPublished` (with rating and vehicleId — used by Search/Reporting), `ReviewHidden`. **Internal only:** `ReviewReplied`.
**Consumes:** `TripCompleted` → create `ReviewWindow`.

---

### 4.10 Support

**Aggregate Root: `SupportTicket`** (general complaints/support)
`Number` (unique, `TK-xxxxx`), `CreatedBy`, `BookingId?`, `Category` (`Payment`, `Vehicle`, `Owner`, `Renter`, `Account`, `Other`), `Subject`, `Priority` (`Low/Normal/High/Urgent`), `Status` (`Open` → `InReview` → `WaitingForCustomer` ⇄ `InReview` → `Resolved` → `Closed`; `Resolved → Open` if reopened within 7 days), `AssignedTo?`, `Messages: List<TicketMessage>` (`AuthorId`, `AuthorRole`, `Body`, `Attachments[]`, `IsInternalNote`, `At`), `ResolvedAt?`.
Behavior: `Open`, `Assign(adminId)`, `AddMessage` (customers cannot see `IsInternalNote`; an admin reply → `WaitingForCustomer`), `Resolve`, `Reopen`, `Close`, `ChangePriority`.

**Aggregate Root: `Dispute`** (booking-related dispute)
`BookingId`, `OpenedBy`, `AgainstUserId`, `Reason` (`Damage`, `LateReturn`, `NoShow`, `FuelOrMileage`, `Other`), `ClaimedAmount?: Money`, `Evidence: List<EvidenceItem>` (`SubmittedBy`, `FileReference|Text`, `At`), `Status` (`Opened` → `UnderReview` → `Resolved`; `Opened → Withdrawn`), `Resolution?` (VO: `Outcome` [`FavorOpener`, `FavorOpponent`, `Split`], `RefundToRenter: Money`, `ChargeToRenter: Money`, `PayoutAdjustment: Money`, `Notes`, `DecidedBy`, `DecidedAt`).
**Invariants:** each booking has at most one unresolved dispute at a time; only parties to the booking or an admin may add evidence; `Resolve` requires `UnderReview` and a named decision maker.
**Published events (2):** `DisputeOpened`, `DisputeResolved` (carries `Resolution` so Payments can handle the money). **Internal only:** `TicketOpened`, `TicketResolved` (ticket replies are shown in the Support UI via SignalR; add a published `TicketReplied` later if email notifications are needed).
**Consumes:** `TripCompleted` (allows opening a dispute within `[ASSUMPTION]` 72 hours); `ReturnCompleted` (if `TripSummary.NewDamages` is not empty, suggest or auto-open a dispute when configured).

---

### 4.11 Notifications

**Aggregate Root: `Notification`**: `RecipientId`, `TemplateKey` (e.g. `booking.approved`), `Title`, `Body`, `Data` (JSON: `bookingId`, deep link, ...), `Channels: List<ChannelDelivery>` (child entity: `Channel` [`InApp`, `Email`, `Push`], `Status` [`Pending`, `Sent`, `Failed`, `Skipped`], `Attempts`, `LastError?`, `SentAt?`), `ReadAt?`, `CreatedAt`, `DedupKey` (unique, prevents duplicate sends when events are redelivered).
Behavior: `Create(recipient, template, data, channels, dedupKey, now)` (skips channels the user has disabled), `MarkSent(channel, now)`, `MarkFailed(channel, error)` (max 3 retries), `MarkRead(now)`.
**Aggregate Root: `NotificationPreference`**: `UserId`, `Rules` (`TemplateGroup` × `Channel` → on/off); important transactional notifications (payment, cancellation) **cannot disable the InApp channel**. Behavior: `Enable`, `Disable`.
**Entity: `DeviceToken`** (`UserId`, `Platform`, `Token`, `LastSeenAt`): `Register`, `Revoke`.
**Consumes:** Users: `IdentityReviewed`, `DriverLicenseReviewed`, `UserSuspended`; Fleet: `VehicleApproved`, `VehicleRejected`, `VehicleStatusChanged`; Booking: `BookingRequested`, `BookingApproved`, `BookingRejected`, `BookingConfirmed`, `BookingCancelled`, `BookingExpired`, `TripStarted`, `TripCompleted`; Payments: `PaymentSucceeded`, `PaymentFailed`, `RefundIssued`, `PayoutPaid`; Handover: `PickupCompleted`, `ReturnCompleted`; Reviews: `ReviewPublished`; Support: `DisputeOpened`, `DisputeResolved`. Handlers map event → `Notification.Create`. The InApp channel is pushed in real time through **SignalR**.

---

### 4.12 Audit

**Aggregate Root (append-only): `AuditEntry`**: `Id`, `OccurredAt`, `ActorId?`, `ActorType` (`User`, `Admin`, `System`), `SourceModule`, `Action` (e.g. `identity.approved`, `vehicle.rejected`, `document.viewed`), `EntityType`, `EntityId`, `Changes?` (JSON before/after with sensitive data filtered out), `IpAddress?`, `UserAgent?`, `CorrelationId`.
**Invariants:** only a `Record(...)` factory; **no update/delete methods**, the repository only exposes `Add` and queries; the DB role forbids `UPDATE/DELETE`.
**Input (no business-event subscriptions):** Audit does **not** subscribe to business integration events. Instead, `Audit.Contracts` exposes one message, `RecordAuditEntry`, which is sent (through the outbox) by an Application-layer pipeline behavior of each module for: (a) every command marked `IAuditedCommand` (all admin actions, role/status changes, approvals/rejections), and (b) every access to a sensitive document (CCCD/GPLX/receipt). This also captures actions that raise no domain event. Never store full CCCD/driver-license numbers in `Changes`.

---

### 4.13 Reporting (read model)

> Only idempotent projections; **never** query other schemas. Each projection has a `processed_events (EventId PK)` table to prevent duplicate processing.

| Projection | Key | Main fields | Updated from events |
|---|---|---|---|
| `DailyPlatformStats` | `Date` | `BookingsRequested/Confirmed/Cancelled/Completed`, `GrossRevenue`, `PlatformFeeRevenue`, `NewUsers`, `NewVehicles` | Booking*, Payment*, UserRegistered, VehicleApproved |
| `OwnerEarningsSummary` | `OwnerId`, `Month` | `Trips`, `GrossAmount`, `NetAmount`, `PaidOut`, `Pending` | TripCompleted, PayoutScheduled/Paid |
| `VehiclePerformance` | `VehicleId`, `Month` | `Trips`, `BookedDays`, `Revenue`, `AverageRating`, `CancellationCount` | Booking*, ReviewPublished |
| `DisputeStats` | `Month` | `Opened`, `Resolved`, `AvgResolutionHours`, `TotalRefunded` | Dispute* |

Each projection has `Apply(<Event>)` methods that mutate internal state (no public setters) and return `bool changed`.

---

## 5. Integration Event Catalog (lives in the publishing module's `*.Contracts`)

Every integration event is an immutable `record` with `EventId`, `OccurredAt`, and contains only IDs plus the data that is needed (never entities). **28 published events in total.** Events not listed here are internal domain events (see the *Internal only* notes in Section 4).

| Event | Published by | Main consumers |
|---|---|---|
| `UserRegistered` | Users | Notifications, Reporting |
| `IdentityReviewed`, `DriverLicenseReviewed`, `UserSuspended` | Users | Notifications |
| `VehicleApproved` | Fleet | Search, Notifications, Reporting |
| `VehicleRejected` | Fleet | Notifications |
| `VehicleListingChanged` | Fleet | Search |
| `VehicleStatusChanged` | Fleet | Search, Availability (release blocks on `Archived`), Notifications |
| `PricingPlanChanged` | Pricing | Search |
| *(none)* | Availability | — |
| `BookingRequested` | Booking | Notifications, Reporting |
| `BookingApproved` | Booking | Payments, Notifications |
| `BookingRejected` | Booking | Availability, Notifications, Reporting |
| `BookingConfirmed` | Booking | Availability, Handover, Pricing, Notifications, Reporting |
| `BookingCancelled` | Booking | Availability, Payments, Handover, Pricing, Notifications, Reporting |
| `BookingExpired` | Booking | Availability, Payments, Pricing, Notifications, Reporting |
| `TripStarted` | Booking | Notifications |
| `TripCompleted` | Booking | Payments, Reviews, Support, Search, Notifications, Reporting |
| `PaymentSucceeded` | Payments | Booking, Notifications, Reporting |
| `PaymentFailed`, `RefundIssued` | Payments | Notifications, Reporting |
| `PayoutScheduled` | Payments | Reporting |
| `PayoutPaid` | Payments | Notifications, Reporting |
| `PickupCompleted` | Handover | Booking, Notifications |
| `ReturnCompleted` | Handover | Booking, Support, Notifications |
| `ReviewPublished` | Reviews | Search, Reporting, Notifications |
| `ReviewHidden` | Reviews | Search |
| `DisputeOpened` | Support | Payments, Notifications, Reporting |
| `DisputeResolved` | Support | Payments, Notifications, Reporting |

**Not events:** Audit entries are produced through the `RecordAuditEntry` message (see 4.12), not through business events.

---

## 6. Key Business Flows (for agents to verify integration)

1. **Place a booking:** the renter calls `POST booking` → Booking checks `IUsersApi` (eligibility) → `IPricingApi` (Quote) → `IAvailabilityApi.PlaceHold` (TTL 12h) → creates `Booking(Requested)` → `BookingRequested` → Notifications informs the owner.
2. **Owner approves:** `Approve` → Application extends the hold to `PaymentDeadline` → `BookingApproved` → Payments creates `Payment(Pending)` + Notifications informs the renter.
3. **Payment:** gateway webhook / manual receipt → `PaymentSucceeded` → `Booking.MarkPaid` (`Confirmed`) → Availability `ConfirmHold` → Handover creates its record → Pricing `Redeem` promo.
4. **Pickup:** both parties confirm the inspection record → `PickupCompleted` → `Booking.StartTrip`.
5. **Return:** `ReturnCompleted` → `Booking.CompleteTrip` → `TripCompleted` → Reviews opens a `ReviewWindow`, Payments schedules the `OwnerPayout`, Reporting updates.
6. **Cancel/expire:** `BookingCancelled/Expired/Rejected` → Availability `Release`; Payments cancels the pending `Payment` (`Expired`/`Cancelled`) or refunds per `RefundPercent` (`Cancelled` after payment).
7. **Dispute:** `DisputeOpened` → Payments holds the payout → `DisputeResolved` → refund / extra charge / release the payout.

---

## 7. Suggested Implementation Order for Sub-Agents

| Phase | Work | Parallelizable? |
|---|---|---|
| 0 | `Shared.Kernel` (Section 2), NetArchTest rules, solution template, Wolverine outbox | No (do first) |
| 1 | **Users**, **Fleet**, **Pricing**, **Availability** | Yes (4 independent agents) |
| 2 | **Booking**, **Payments** (need phase 1 Contracts) | Yes |
| 3 | **Handover**, **Reviews**, **Support** | Yes |
| 4 | **Search**, **Notifications**, **Audit**, **Reporting** (mostly event consumers) | Yes |

## 8. Definition of Done for Each Module

- [ ] Aggregates/entities/VOs match the description, **no public setters**, every transition goes through a method.
- [ ] Domain unit tests cover valid/invalid transitions, invariants, and domain events.
- [ ] EF configuration + migration in the module's own schema; no FK outside the schema.
- [ ] `*.Contracts` contains only the interfaces/DTOs/integration events that must be public.
- [ ] Event-consuming handlers are **idempotent** (with a test that invokes them twice).
- [ ] Integration tests (Testcontainers) for persistence + outbox.
- [ ] NetArchTest passes: no cross-module references other than Contracts.
- [ ] The module's admin endpoints have the `Admin` policy; sensitive actions emit Audit entries.

## 9. Open Points for the Team to Confirm

1. The order **approve → pay** (currently following the use case) versus **pay → approve**: affects only the `Booking` state machine and the Payments event flow.
2. The meaning of the **deposit** (partial booking payment vs. refundable security deposit): affects `PaymentPurpose` and `PricingSnapshot`.
3. Whether `Shared.Kernel` may be referenced by all modules (currently assumed yes).
4. Event policy: confirm the reduced list of 28 published events (Section 5); re-add an internal event as a published one only when a real consumer exists.
5. The `[ASSUMPTION]` values (15-minute quote TTL, 2-hour payment deadline, 2-hour buffer, 14-day review window, minimum age 21, ...) should be moved to `appsettings`/a configuration table.