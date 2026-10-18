# Pricing Module — Implementation Plan

> **Module Directory:** `src/Modules/Pricing`  
> **Projects:** `CarRental.Modules.Pricing.csproj`, `CarRental.Modules.Pricing.Contracts.csproj`  
> **Dedicated Git Branch:** `feat/module-pricing`  
> **Phase:** 1 (Catalog & Core Foundation — Parallel Stream 3)  
> **PostgreSQL Schema:** `pricing`  
> **References:** [be/docs/domain-model-entities-updated.md](../../../docs/domain-model-entities-updated.md) (Section 4.2 & 5)

---

## 1. Objectives & Scope
The Pricing module handles dynamic rental price calculation, seasonal surcharge/discount rules, weekend pricing, security deposits, promotions/voucher codes, cancellation policies, and immutable price snapshots (`Quote`).

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
- **Shared Kernel:** `CarRental.BuildingBlocks` (Money, DateTimeRange, Result, Guard, AggregateRoot).
- **Consumes Events from:**
  - `Bookings`: `BookingConfirmed` (to trigger promotion redemption), `BookingCancelled` / `BookingExpired` (to release promotion redemption if applicable).
- **Downstream Consumers of Pricing Contracts:**
  - `Bookings`: Calls `IPricingApi.GetQuoteAsync` during booking placement.
  - `Search`: Consumes `PricingPlanChanged` to refresh vehicle search cards.

---

## 4. Contracts Project (`CarRental.Modules.Pricing.Contracts`)
### 4.1 Synchronous Interface (`IPricingApi`)
```csharp
public interface IPricingApi
{
    Task<Result<QuoteDto>> GetQuoteAsync(
        Guid vehicleId, 
        Guid renterId, 
        DateTimeRange period, 
        string? promoCode = null, 
        CancellationToken ct = default);

    Task<Result> ConsumeQuoteAsync(Guid quoteId, CancellationToken ct = default);
}
```

### 4.2 DTOs
- `QuoteDto(Guid QuoteId, Guid VehicleId, Money Total, Money DepositAmount, Money PlatformFee, Money OwnerEarning, IReadOnlyList<QuoteLineDto> Lines, CancellationPolicyDto Policy, DateTimeOffset ExpiresAt)`
- `QuoteLineDto(string Type, string Description, Money Amount)`
- `CancellationPolicyDto(IReadOnlyList<CancellationTierDto> Tiers)`
- `CancellationTierDto(int HoursBeforeStart, decimal RefundPercent)`

### 4.3 Published Integration Events (1 Published Event)
- `PricingPlanChanged(Guid EventId, DateTimeOffset OccurredAt, Guid VehicleId, Money BaseDailyRate)`

*(Note: `PromotionRedeemed` is an internal domain event).*

---

## 5. Domain Layer (`Entities/`, `ValueObjects/`, `Enums/`)

### 5.1 Enums & Value Objects
- `PromotionType` (`Percentage`, `FixedAmount`)
- `QuoteLineType` (`BaseRate`, `WeekendSurcharge`, `WeeklyDiscount`, `MonthlyDiscount`, `SeasonalSurcharge`, `CleaningFee`, `PromotionDiscount`)
- `CancellationPolicy(IReadOnlyList<CancellationTier> Tiers)`:
  - Invariant: Tiers must be monotonic descending by `HoursBeforeStart` (e.g. `> 48h -> 100%`, `24h..48h -> 50%`, `< 24h -> 0%`).
  - Method: `decimal GetRefundPercentage(TimeSpan timeUntilPickup)`.

### 5.2 Aggregate Root: `PricingPlan`
- Properties:
  - `Guid VehicleId` (Unique)
  - `Money BaseDailyRate` (> 0)
  - `decimal WeekendSurchargePercent` (0..100)
  - `decimal WeeklyDiscountPercent`, `decimal MonthlyDiscountPercent`
  - `Money SecurityDeposit`
  - `Money? CleaningFee`, `Money? DeliveryFeePerKm`
  - `int? DailyKmLimit`, `Money? ExtraKmFee`
  - `List<SeasonalPriceRule> SeasonalRules`: Invariant: Seasons must not overlap.
  - `CancellationPolicy CancellationPolicy`
- Method:
  - `Result<Quote> CalculateQuote(Guid renterId, DateTimeRange period, Promotion? promotion, DateTimeOffset now, PlatformFeePolicy feePolicy)`:
    - Pure calculation function with zero side effects.
    - Produces itemized `QuoteLine` breakdown.

### 5.3 Aggregate Root / Entity: `Quote`
- Persisted quote with 15-minute TTL.
- `bool IsValid(DateTimeOffset now) => !IsConsumed && now <= ExpiresAt;`
- `Result Consume(DateTimeOffset now)`: Marks quote as consumed (single-use guarantee).

### 5.4 Aggregate Root: `Promotion`
- Properties: `string Code`, `PromotionType Type`, `decimal Value`, `Money? MaxDiscount`, `Money? MinBookingValue`, `DateTimeRange ValidPeriod`, `int? TotalUsageLimit`, `int PerUserLimit`, `bool IsActive`.
- Methods: `Validate(userId, bookingValue, now)`, `Redeem(userId, bookingId, now)`, `Deactivate()`.

---

## 6. Persistence & EF Core (`Data/`)
- `PricingDbContext`: `HasDefaultSchema("pricing")`.
- `IEntityTypeConfiguration<PricingPlan>`:
  - Unique index on `VehicleId`.
  - Owned entities for `Money` and `CancellationPolicy`.
  - Concurrency token `xmin`.
- `IEntityTypeConfiguration<Quote>`:
  - Index on `ExpiresAt` for TTL cleanup.
- `IEntityTypeConfiguration<Promotion>`:
  - Unique index on `Code`.
- Migration: `AddPricingInitialMigration`.

---

## 7. Event Handlers & Endpoints (`EventHandlers/`, `Endpoints/`)
- `BookingConfirmedHandler`: Consumes `BookingConfirmed` -> Invokes `Promotion.Redeem(...)`.
- `BookingCancelledHandler`: Releases promo redemption count if applicable.
- Minimal API Endpoints (`MapGroup("/api/pricing")`):
  - `POST /plans` (Set or update vehicle pricing)
  - `GET /plans/{vehicleId}`
  - `POST /quotes` (Calculate transient quote)
  - `POST /promotions` (Admin create promo)
  - `GET /promotions/validate?code=...`

---

## 8. Step-by-Step Task Checklist
- [ ] Implement `CancellationPolicy` and `QuoteLine` value objects.
- [ ] Implement `SeasonalPriceRule` child entity.
- [ ] Implement `PricingPlan` aggregate root with pure quote calculation logic.
- [ ] Implement `Quote` and `Promotion` aggregates.
- [ ] Implement `IPricingApi` service and DTOs in Contracts.
- [ ] Setup `PricingDbContext`, EF Core mappings, and initial migration.
- [ ] Implement Wolverine integration event handlers for promotions.
- [ ] Setup Minimal API endpoints.
- [ ] Unit tests:
  - `CalculateQuote_WithWeekendAndWeeklyDiscount_AppliesCorrectAmounts`
  - `AddSeasonalRule_OverlappingDates_ShouldFail`
  - `CancellationPolicy_TiersNotMonotonic_ShouldThrowDomainException`
  - `Promotion_RedeemExceedingPerUserLimit_ShouldFail`
- [ ] Testcontainers integration tests.
