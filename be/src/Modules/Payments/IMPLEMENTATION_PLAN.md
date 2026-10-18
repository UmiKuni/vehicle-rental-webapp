# Payments Module — Implementation Plan

> **Module Directory:** `src/Modules/Payments`  
> **Projects:** `CarRental.Modules.Payments.csproj`, `CarRental.Modules.Payments.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-payments`  
> **Phase:** 2 (Rental Flow Core — Parallel Stream 2)  
> **PostgreSQL Schema:** `payments`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.4 & 5)

---

## 1. Objectives & Scope
The Payments module is responsible for payment collection (via sandbox gateway or manual bank transfer receipt), idempotency in webhook processing, automated refund calculations upon cancellation, and owner payout scheduling with dispute hold mechanisms.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1 contracts are available
git checkout -b feat/module-payments

# Build module
dotnet build src/Modules/Payments/CarRental.Modules.Payments/CarRental.Modules.Payments.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Money, FileReference, Result, Guard, AggregateRoot).
- **Consumes Events from:**
  - `Bookings`: `BookingApproved` (initiates pending deposit payment), `BookingExpired` (cancels pending payment), `BookingCancelled` (cancels pending payment or triggers refund per refund percent), `TripCompleted` (schedules owner payout).
  - `Support`: `DisputeOpened` (places payout on hold), `DisputeResolved` (adjusts/releases payout or executes refunds/charges).
- **Downstream Consumers of Payments Contracts:**
  - `Bookings`: Consumes `PaymentSucceeded` to advance booking to `Confirmed`.
  - `Notifications`, `Reporting`.

---

## 4. Contracts Project (`CarRental.Modules.Payments.Contracts`)
### 4.1 Synchronous Interface (`IPaymentsApi`)
```csharp
public interface IPaymentsApi
{
    Task<Result<PaymentStatusDto>> GetPaymentStatusAsync(Guid paymentId, CancellationToken ct = default);
}
```

### 4.2 Published Integration Events (5 Published Events)
- `PaymentSucceeded(Guid EventId, DateTimeOffset OccurredAt, Guid PaymentId, Guid BookingId, Money Amount, string Method)`
- `PaymentFailed(Guid EventId, DateTimeOffset OccurredAt, Guid PaymentId, Guid BookingId, string FailureReason)`
- `RefundIssued(Guid EventId, DateTimeOffset OccurredAt, Guid RefundId, Guid PaymentId, Guid BookingId, Money RefundAmount)`
- `PayoutScheduled(Guid EventId, DateTimeOffset OccurredAt, Guid PayoutId, Guid BookingId, Guid OwnerId, Money NetAmount, DateTimeOffset ScheduledFor)`
- `PayoutPaid(Guid EventId, DateTimeOffset OccurredAt, Guid PayoutId, Guid BookingId, Money NetAmount)`

*(Note: `PaymentExpired` is an internal domain event; Booking enforces its own `PaymentDeadline` and publishes `BookingExpired`).*

---

## 5. Domain Layer (`Entities/`, `Enums/`, `ValueObjects/`)

### 5.1 Enums & Strongly-Typed IDs
- `PaymentPurpose` (`BookingDeposit`, `RentalBalance`, `DamageCharge`)
- `PaymentMethod` (`Gateway`, `ManualReceipt`)
- `PaymentStatus` (`Pending`, `Succeeded`, `Failed`, `Expired`, `Cancelled`, `PartiallyRefunded`, `Refunded`)
- `PayoutStatus` (`Scheduled`, `Paid`, `Failed`, `OnHold`)

### 5.2 Child Entities & Value Objects
- `PaymentAttempt`: `Guid Id`, `string Provider`, `string? ProviderTxnId`, `string Status`, `string? RedirectUrl`, `string? FailureCode`, `DateTimeOffset CreatedAt`, `DateTimeOffset? CompletedAt`.
- `ManualReceipt`: `FileReference ProofImage`, `string Status` (`Submitted`, `Approved`, `Rejected`), `Guid? ReviewedBy`, `string? RejectReason`.
- `Refund`: `Guid Id`, `Money Amount`, `string Reason`, `string Status`, `string? ProviderRefundId`.

### 5.3 Aggregate Root: `Payment`
- Properties: `Guid BookingId`, `Guid PayerId`, `PaymentPurpose Purpose`, `Money Amount`, `PaymentMethod Method`, `PaymentStatus Status`, `string IdempotencyKey`, `DateTimeOffset ExpiresAt`.
- Business Methods:
  - `static Result<Payment> Create(...)`
  - `Result StartGatewayAttempt(string provider, DateTimeOffset now)`
  - `Result RecordGatewayResult(string providerTxnId, bool success, string? rawCode, DateTimeOffset now)`:
    - **Idempotency Guarantee**: If already `Succeeded`, repeated webhook must return success without emitting duplicate events.
  - `Result SubmitReceipt(FileReference proof, DateTimeOffset now)`
  - `Result ApproveReceipt(Guid adminId, DateTimeOffset now)`
  - `Result RejectReceipt(Guid adminId, string reason, DateTimeOffset now)`
  - `Result RequestRefund(Money amount, string reason, DateTimeOffset now)`:
    - Invariant: Cumulative refunds must not exceed collected amount.

### 5.4 Aggregate Root: `OwnerPayout`
- Properties: `Guid BookingId` (Unique), `Guid OwnerId`, `Money GrossAmount`, `Money PlatformFee`, `Money NetAmount`, `PayoutStatus Status`, `DateTimeOffset ScheduledFor`, `PayoutAccount BankAccount`.
- Methods: `Schedule(...)`, `Hold()` (triggered by dispute), `Release()`, `MarkPaid()`, `MarkFailed()`.

---

## 6. Infrastructure & Adapters (`Gateways/`, `Data/`)
- `IPaymentGateway`: Isolated domain abstraction.
- `SandboxPaymentGateway`: Simulated gateway adapter for test and local environments.
- `PaymentsDbContext`: `HasDefaultSchema("payments")`.
- `IEntityTypeConfiguration<Payment>`:
  - Unique index on `IdempotencyKey`.
  - Unique index on `(Provider, ProviderTxnId)`.
  - Concurrency token `xmin`.
- Migration: `AddPaymentsInitialMigration`.

---

## 7. Event Handlers & Webhooks (`EventHandlers/`, `Endpoints/`)
- `BookingApprovedHandler`: Consumes `BookingApproved` -> Creates `Payment` with `Pending` status.
- `BookingCancelledHandler`: Consumes `BookingCancelled` -> Calls `Payment.RequestRefund(Amount * RefundPercent)`.
- `TripCompletedHandler`: Consumes `TripCompleted` -> Schedules `OwnerPayout`.
- Minimal API Endpoints:
  - `POST /api/payments/checkout` (Generates gateway payment URL)
  - `POST /api/payments/webhook` (Idempotent gateway callback receiver)
  - `POST /api/payments/{id}/receipt` (Upload transfer screenshot)
  - `POST /api/payments/admin/receipts/{id}/approve` (Admin manual approval)

---

## 8. Step-by-Step Task Checklist
- [ ] Implement Enums, `PaymentAttempt`, `ManualReceipt`, `Refund`.
- [ ] Implement `Payment` aggregate root with idempotent webhook processing and refund invariants.
- [ ] Implement `OwnerPayout` aggregate root.
- [ ] Define `IPaymentGateway` abstraction and sandbox implementation.
- [ ] Setup `PaymentsDbContext` and EF Core mappings.
- [ ] Create EF Core migration for `payments` schema.
- [ ] Implement Wolverine event consumers for booking and dispute events.
- [ ] Implement Webhook and Manual Receipt endpoints.
- [ ] Unit tests:
  - `RecordGatewayResult_CalledMultipleTimes_IsIdempotent`
  - `RequestRefund_ExceedingTotalAmount_ShouldFail`
  - `ApproveReceipt_TransitionsToSucceededAndRaisesPaymentSucceeded`
  - `OwnerPayout_Hold_TransitionsToOnHold`
- [ ] Testcontainers integration tests.
