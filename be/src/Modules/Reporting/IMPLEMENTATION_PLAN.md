# Reporting Module — Implementation Plan

> **Module Directory:** `src/Modules/Reporting`  
> **Projects:** `CarRental.Modules.Reporting.csproj`, `CarRental.Modules.Reporting.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-reporting`  
> **Phase:** 4 (Projections & Platform — Parallel Stream 4)  
> **PostgreSQL Schema:** `reporting`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.13 & 5)

---

## 1. Objectives & Scope
The Reporting module maintains pre-aggregated, read-optimized analytical projections for platform administrators and vehicle owners. It isolates high-volume reporting reads from the core transactional schemas, using an idempotent event inbox (`processed_events`).

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after Phase 1 & 2 contracts are available
git checkout -b feat/module-reporting

# Build module
dotnet build src/Modules/Reporting/CarRental.Modules.Reporting/CarRental.Modules.Reporting.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Money, Result).
- **Consumes Events from (aligned with 28-Event Catalog):**
  - `Bookings`: `BookingRequested`, `BookingRejected`, `BookingConfirmed`, `BookingCancelled`, `BookingExpired`, `TripCompleted`.
  - `Payments`: `PaymentSucceeded`, `PaymentFailed`, `RefundIssued`, `PayoutScheduled`, `PayoutPaid`.
  - `Users`: `UserRegistered`.
  - `Fleet`: `VehicleApproved`.
  - `Reviews`: `ReviewPublished`.
  - `Support`: `DisputeOpened`, `DisputeResolved`.

---

## 4. Contracts Project (`CarRental.Modules.Reporting.Contracts`)
### 4.1 Synchronous Interface (`IReportingApi`)
```csharp
public interface IReportingApi
{
    Task<Result<OwnerEarningsDto>> GetOwnerEarningsAsync(Guid ownerId, int year, int month, CancellationToken ct = default);
    Task<Result<PlatformSummaryDto>> GetPlatformSummaryAsync(DateOnly from, DateOnly to, CancellationToken ct = default);
}
```

---

## 5. Domain & Read Models (`Projections/`)

### 5.1 Projection: `DailyPlatformStats`
- Primary Key: `DateOnly Date`
- Metrics: `int BookingsRequested`, `int BookingsConfirmed`, `int BookingsCancelled`, `int BookingsCompleted`, `Money GrossRevenue`, `Money PlatformFeeRevenue`, `int NewUsers`, `int NewVehicles`.
- Encapsulated Method: `bool Apply(IDomainEvent e)`

### 5.2 Projection: `OwnerEarningsSummary`
- Composite Key: `(Guid OwnerId, int Year, int Month)`
- Metrics: `int Trips`, `Money GrossAmount`, `Money NetAmount`, `Money PaidOut`, `Money Pending`.
- Encapsulated Method: `bool Apply(IDomainEvent e)`

### 5.3 Projection: `VehiclePerformance`
- Composite Key: `(Guid VehicleId, int Year, int Month)`
- Metrics: `int Trips`, `int BookedDays`, `Money Revenue`, `decimal AverageRating`, `int CancellationCount`.
- Encapsulated Method: `bool Apply(IDomainEvent e)`

### 5.4 Projection: `DisputeStats`
- Key: `(int Year, int Month)`
- Metrics: `int Opened`, `int Resolved`, `double AvgResolutionHours`, `Money TotalRefunded`.

---

## 6. Persistence & EF Core (`Data/`)
- `ReportingDbContext`: `HasDefaultSchema("reporting")`.
- Idempotency Table:
  ```csharp
  public class ProcessedEvent
  {
      public Guid EventId { get; set; }
      public DateTimeOffset ProcessedAt { get; set; }
  }
  ```
- EF Core Mapping:
  - Configure primary keys and monthly indexes for fast dashboard queries.
- Migration: `AddReportingInitialMigration`.

---

## 7. Event Handlers & Endpoints (`EventHandlers/`, `Endpoints/`)
- Idempotent Wolverine Consumer Pattern:
  ```csharp
  public async Task Handle(TripCompleted e)
  {
      if (await _dbContext.ProcessedEvents.AnyAsync(x => x.EventId == e.EventId))
          return;

      // Update OwnerEarningsSummary & VehiclePerformance
      _dbContext.ProcessedEvents.Add(new ProcessedEvent { EventId = e.EventId, ProcessedAt = DateTimeOffset.UtcNow });
      await _dbContext.SaveChangesAsync();
  }
  ```
- Minimal API Endpoints:
  - `GET /api/reports/owner/earnings` (Owner personal earnings breakdown)
  - Admin endpoints (`RequireAuthorization("Admin")`):
    - `GET /api/admin/reports/platform-stats`
    - `GET /api/admin/reports/vehicle-performance`
    - `GET /api/admin/reports/disputes`

---

## 8. Step-by-Step Task Checklist
- [ ] Implement `ProcessedEvent` entity for idempotent event processing.
- [ ] Implement Projections: `DailyPlatformStats`, `OwnerEarningsSummary`, `VehiclePerformance`, `DisputeStats`.
- [ ] Implement `IReportingApi` in `CarRental.Modules.Reporting.Contracts`.
- [ ] Setup `ReportingDbContext` and EF Core mappings.
- [ ] Create EF Core migration for `reporting` schema.
- [ ] Implement Wolverine event consumers with deduplication checks.
- [ ] Build Minimal API reporting endpoints.
- [ ] Unit tests for projection `Apply` methods.
- [ ] Integration tests verifying that duplicate events produce zero state skew.
