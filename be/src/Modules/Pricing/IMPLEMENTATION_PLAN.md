# Pricing Module — Implementation Plan

> **Module Directory:** `src/Modules/Pricing`  
> **Projects:** `CarRental.Modules.Pricing.csproj`, `CarRental.Modules.Pricing.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-pricing`  
> **Phase:** 1 (Catalog & Core Foundation — Parallel Stream 3)  
> **PostgreSQL Schema:** `pricing`  
> **References:** [be/docs/domain-model-entities-reworked.md](../../../docs/domain-model-entities-reworked.md) (Section 4.2 & 5)

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
| **Aggregate Root** | `PricingPlan` with `BaseDailyRate`, `WeekendSurchargePercent`, `WeeklyDiscountPercent`, `MonthlyDiscountPercent`, `SeasonalRules`, `CancellationPolicy` VO | `PricingPlan` (table `PricingPlans`) with `Id`, `VehicleId` (unique), `WeekdayRate`, `WeekendRate`, `DepositAmount`, `KmLimit` (per day). |
| **Child Entities** | None (complex value objects and separate aggregates) | `FeeRule` (table `FeeRules`, `Type`: `OverKm`/`Cleaning`, `Amount`), `PricingPlanVersion` (table `PricingPlanVersions`, `VehicleId`, `Config` JSONB, `CreatedAt` append-only audit trail for NFR-13). |
| **Quote Persistence** | Stored `Quote` aggregate root with 15m TTL | Pure Value Object `Quote`, **not stored** in pricing database (the booking stores snapshot line items as `BookingLineItems`). |
| **Promotions** | Stored `Promotion` aggregate root with usage counters | Section 10 [EXT] (optional extension — not implemented by default). |
| **Published Events** | `PricingPlanChanged` (BaseDailyRate) | `PricingUpdated` (carries `VehicleId`, `PriceFrom = min(WeekdayRate, WeekendRate)`). |
| **Contracts** | `IPricingApi.GetQuoteAsync`, `ConsumeQuoteAsync` | `IPricingApi.GetQuoteAsync(vehicleId, period)` returning `QuoteDto`. |
| **Table Naming** | snake_case tables | Retained snake_case tables (`pricing_plans`, `fee_rules`, `pricing_plan_versions`) matching PostgreSQL conventions. |

---

## 1. Objectives & Scope
The Pricing module calculates rental quotes based on weekday and weekend rates, manages mandatory security deposits, daily kilometer allowances, and fee rules (over-km fee and cleaning fee). It produces immutable `Quote` calculation results for the Booking module and preserves an append-only audit version history in `PricingPlanVersions` (NFR-13).

---

## 2. Git Branch & Setup Instructions
```bash
# Branch out from main after feat/shared-kernel is merged
git checkout -b feat/module-pricing

# Build module
dotnet build src/Modules/Pricing/CarRental.Modules.Pricing/CarRental.Modules.Pricing.csproj
```

---

## 3. Dependencies
- **Shared Kernel:** `CarRental.BuildingBlocks` (Money, DateTimeRange, Result, Guard, AggregateRoot, Entity).
- **Contracts Referenced:** `CarRental.Modules.Fleet.Contracts` (`IFleetApi.GetVehicleSummaryAsync` for owner validation).
- **Downstream Consumers of Pricing Contracts:**
  - `Bookings`: Calls `IPricingApi.GetQuoteAsync` during booking placement.
  - `Search`: Consumes `PricingUpdated` to update min-price filters.

---

## 4. Contracts Project (`CarRental.Modules.Pricing.Contracts`)

### 4.1 Synchronous Interface (`IPricingApi`)
```csharp
public interface IPricingApi
{
    Task<Result<QuoteDto>> GetQuoteAsync(
        Guid vehicleId, 
        DateTimeRange period, 
        CancellationToken ct = default);
}
```

### 4.2 DTOs
- `QuoteDto(Guid VehicleId, Money Total, Money Deposit, IReadOnlyList<QuoteLineDto> Lines, int KmLimitPerDay, Money OverKmFeePerKm, CancellationPolicyDto Policy)`
- `QuoteLineDto(string Type, string Description, Money Amount)`
- `CancellationPolicyDto(IReadOnlyList<CancellationTierDto> Tiers)`
- `CancellationTierDto(int HoursBeforeStart, decimal PenaltyPercent)`

### 4.3 Published Integration Events (1 Published Event)
- `PricingUpdated(Guid EventId, DateTimeOffset OccurredAt, Guid VehicleId, Money PriceFrom)`

---

## 5. Domain Layer (`Entities/`, `Enums/`, `ValueObjects/`)

### 5.1 Enums & Value Objects
- `FeeType`: `OverKm`, `Cleaning`.
- `Quote` (Value Object — not persisted):
  - Properties: `IReadOnlyList<QuoteLine> Lines`, `Money Total`, `int KmLimitPerDay`, `Money OverKmFeePerKm`, `CancellationPolicy Policy`.
  - Calculation logic: Rent sum per day (Saturday/Sunday uses `WeekendRate`, other days use `WeekdayRate`, UTC+7, partial day counts as full day), Deposit (`DepositAmount`), CleaningFee (if cleaning rule configured).
- `CancellationPolicy` (Value Object):
  - Taken from platform configuration (default tiers: e.g. `> 48h -> 0% penalty`, `24h..48h -> 50% penalty`, `< 24h -> 100% penalty`).

### 5.2 Child Entities
- **`FeeRule`** (Table `FeeRules`):
  - Properties: `Guid Id`, `Guid PricingPlanId`, `FeeType Type`, `Money Amount`.
  - Invariant: At most one rule per `FeeType`.
- **`PricingPlanVersion`** (Table `PricingPlanVersions`):
  - Properties: `Guid Id`, `Guid VehicleId`, `string ConfigJson` (JSONB snapshot), `DateTimeOffset CreatedAt`.
  - Invariant: Appended on **every** pricing change for audit (NFR-13); never loaded into memory with the aggregate root.

### 5.3 Aggregate Root: `PricingPlan`
- Table `PricingPlans`:
  - `Guid Id`
  - `Guid VehicleId` (unique, indexed)
  - `Money WeekdayRate` (> 0)
  - `Money WeekendRate` (> 0)
  - `Money DepositAmount` (>= 0)
  - `int KmLimit` (> 0 per day)
  - Child collection: `IReadOnlyCollection<FeeRule> FeeRules`.
- Domain Invariants & Methods:
  ```csharp
  static Result<PricingPlan> Create(Guid vehicleId, Money weekdayRate, Money weekendRate, Money depositAmount, int kmLimit, DateTimeOffset now);
  Result Update(Money weekdayRate, Money weekendRate, Money depositAmount, int kmLimit, DateTimeOffset now); // appends PricingPlanVersion, raises PricingUpdated
  Result SetFeeRule(FeeType type, Money amount, DateTimeOffset now); // upsert by type, appends version
  Result RemoveFeeRule(FeeType type, DateTimeOffset now);
  Result<Quote> CalculateQuote(DateTimeRange period); // pure calculation, zero side effects
  ```

---

## 6. Persistence & EF Core (`Data/`)
- `PricingDbContext`: `HasDefaultSchema("pricing")`.
- Table mapping (snake_case):
  - `pricing_plans` (Aggregate Root with `xmin` row version)
  - `fee_rules` (FK `pricing_plan_id`)
  - `pricing_plan_versions` (FK `vehicle_id`, JSONB column for `config_json`)
- EF Core entity configurations in `Data/Configurations/`.

---

## 7. Application Services & Endpoints (`Services/`, `Endpoints/`)
- `PricingApiService`: Implements `IPricingApi`.
- Minimal API Endpoints (`MapGroup("/api/pricing")`):
  - `POST /quotes` (body: `vehicleId`, `startTime`, `endTime`; server computes price snapshot)
  - `PUT /vehicles/{id}` (owner configures rates, over-km fee, cleaning fee; verifies ownership via `IFleetApi`)

---

## 8. Step-by-Step Task Checklist
- [x] Document updated architecture and database entity mapping.
- [ ] Implement `PricingPlan` aggregate root, `FeeRule`, `PricingPlanVersion`, and `Quote` value object.
- [ ] Implement `IPricingApi` and DTOs/events in `CarRental.Modules.Pricing.Contracts`.
- [ ] Setup `PricingDbContext` with schema `pricing` and JSONB versioning.
- [ ] Add domain unit tests for weekend/weekday calculation, fee rule upserts, and quote generation.
- [ ] Verify zero EF/ASP.NET dependencies in `Entities/` via `DomainEncapsulationTests`.
