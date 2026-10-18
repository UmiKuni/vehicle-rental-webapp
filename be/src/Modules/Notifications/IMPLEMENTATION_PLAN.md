# Notifications Module — Implementation Plan

> **Module Directory:** `src/Modules/Notifications`  
> **Projects:** `CarRental.Modules.Notifications.csproj`, `CarRental.Modules.Notifications.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-notifications`  
> **Phase:** 4 (Projections & Platform — Parallel Stream 2)  
> **PostgreSQL Schema:** `notifications`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.11 & 5)

---

## 1. Objectives & Scope
The Notifications module is a multi-channel alert engine (InApp with SignalR real-time push, Email, and Mobile Push). It enforces user delivery preferences, delivery retries, deduplication (`DedupKey`), and guarantees that critical transactional notices cannot be suppressed.

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1 & 2 contracts are available
git checkout -b feat/module-notifications

# Build module
dotnet build src/Modules/Notifications/CarRental.Modules.Notifications/CarRental.Modules.Notifications.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Result, Guard, AggregateRoot).
- **Consumes Events from (aligned with 28-Event Catalog):**
  - `Users`: `UserRegistered`, `IdentityReviewed`, `DriverLicenseReviewed`, `UserSuspended`.
  - `Fleet`: `VehicleApproved`, `VehicleRejected`, `VehicleStatusChanged`.
  - `Bookings`: `BookingRequested`, `BookingApproved`, `BookingRejected`, `BookingConfirmed`, `BookingCancelled`, `BookingExpired`, `TripStarted`, `TripCompleted`.
  - `Payments`: `PaymentSucceeded`, `PaymentFailed`, `RefundIssued`, `PayoutPaid`.
  - `Handover`: `PickupCompleted`, `ReturnCompleted`.
  - `Reviews`: `ReviewPublished`.
  - `Support`: `DisputeOpened`, `DisputeResolved`.

---

## 4. Contracts Project (`CarRental.Modules.Notifications.Contracts`)
### 4.1 Synchronous Interface (`INotificationsApi`)
```csharp
public interface INotificationsApi
{
    Task<Result<int>> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
}
```

### 4.2 DTOs
- `NotificationDto(Guid Id, string Title, string Body, string? DataJson, bool IsRead, DateTimeOffset CreatedAt)`

---

## 5. Domain Layer (`Entities/`, `Enums/`, `ValueObjects/`)

### 5.1 Enums & Value Objects
- `DeliveryChannel` (`InApp`, `Email`, `Push`)
- `DeliveryStatus` (`Pending`, `Sent`, `Failed`, `Skipped`)
- `TemplateGroup` (`Bookings`, `Payments`, `Promotions`, `Support`, `Account`)

### 5.2 Child Entities: `ChannelDelivery`, `DeviceToken`
- `ChannelDelivery`: `Guid Id`, `DeliveryChannel Channel`, `DeliveryStatus Status`, `int Attempts`, `string? LastError`, `DateTimeOffset? SentAt`.
- `DeviceToken`: `Guid Id`, `Guid UserId`, `string Platform` (`iOS`, `Android`, `Web`), `string Token`, `DateTimeOffset LastSeenAt`.

### 5.3 Aggregate Root: `Notification`
- Properties:
  - `Guid Id`, `Guid RecipientId`, `string TemplateKey`, `string Title`, `string Body`, `string? DataJson`
  - `List<ChannelDelivery> Channels`
  - `DateTimeOffset? ReadAt`, `DateTimeOffset CreatedAt`
  - `string DedupKey` (Unique idempotency key, e.g. `booking.approved:bk-123:renter-456`)
- Methods:
  - `static Result<Notification> Create(...)`: Evaluates preferences and initializes channel deliveries.
  - `Result MarkSent(DeliveryChannel channel, DateTimeOffset now)`
  - `Result MarkFailed(DeliveryChannel channel, string error)`: Caps retries at 3.
  - `Result MarkRead(DateTimeOffset now)`

### 5.4 Aggregate Root: `NotificationPreference`
- Properties: `Guid UserId`, `List<PreferenceRule> Rules`.
- Rules:
  - Critical transactional notifications (payment, booking status, cancellation) **cannot disable the `InApp` channel**.

---

## 6. Persistence & EF Core (`Data/`)
- `NotificationsDbContext`: `HasDefaultSchema("notifications")`.
- `IEntityTypeConfiguration<Notification>`:
  - Unique index on `DedupKey`.
  - Index on `(RecipientId, ReadAt)`.
- `IEntityTypeConfiguration<DeviceToken>`:
  - Unique index on `Token`.
- Migration: `AddNotificationsInitialMigration`.

---

## 7. Real-Time Hub & Dispatchers (`Hubs/`, `Services/`)
- `NotificationHub`: ASP.NET Core SignalR hub mapping user connections to `userId` groups.
- `EmailNotificationSender` & `PushNotificationSender`: Adapters for external dispatch.
- Wolverine Event Handlers:
  - Map each incoming domain event into a `Notification.Create` command using a deterministic `DedupKey`.
  - Dispatch InApp alert via `IHubContext<NotificationHub>`.

---

## 8. Step-by-Step Task Checklist
- [ ] Implement Enums, `ChannelDelivery`, `DeviceToken`.
- [ ] Implement `Notification` aggregate with `DedupKey` logic and retry rules.
- [ ] Implement `NotificationPreference` aggregate with mandatory transactional rules.
- [ ] Setup `NotificationsDbContext` and EF Core mappings.
- [ ] Create EF Core migration for `notifications` schema.
- [ ] Configure SignalR `NotificationHub`.
- [ ] Implement Wolverine integration event consumers for all modules.
- [ ] Build Minimal API endpoints (`GET /api/notifications`, `POST /api/notifications/{id}/read`, `POST /api/notifications/devices`).
- [ ] Unit tests:
  - `Create_WithDuplicateDedupKey_IsPrevented`
  - `Preference_DisableInAppForCriticalEvent_ShouldFail`
  - `MarkFailed_Exceeding3Retries_Stops`
- [ ] Testcontainers integration tests.
