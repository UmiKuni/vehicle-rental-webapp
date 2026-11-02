using CarRental.Modules.Payments.Errors;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;
using CarRental.BuildingBlocks.Results;
using CarRental.Modules.Payments.Contracts.Enums;

namespace CarRental.Modules.Payments.Entities;

public class Deposit : AggregateRoot<DepositId>
{
    public Guid BookingId { get; private set; }
    public Money Amount { get; private set; } = null!;
    public DepositStatus Status { get; private set; }
    public Money DeductedAmount { get; private set; } = null!;
    public DateTimeOffset? ReleaseAfter { get; private set; }
    public bool IsFrozen { get; private set; }
    public DateTimeOffset? SettledAt { get; private set; }

    private Deposit() { }

    private Deposit(
        DepositId id,
        Guid bookingId,
        Money amount,
        DateTimeOffset now)
    {
        Id = id;
        BookingId = bookingId;
        Amount = amount;
        Status = DepositStatus.Held;
        DeductedAmount = Money.Zero();
        IsFrozen = false;
        ReleaseAfter = null;
    }

    public static Result<Deposit> Hold(Guid bookingId, Money amount, DateTimeOffset now)
    {
        if (bookingId == Guid.Empty)
            return Result.Failure<Deposit>(PaymentsErrors.DepositInvalidBooking);

        if (amount == null || amount.Amount < 0)
            return Result.Failure<Deposit>(PaymentsErrors.DepositInvalidAmount);

        return Result.Success(new Deposit(DepositId.New(), bookingId, amount, now));
    }

    public void Freeze()
    {
        IsFrozen = true;
    }

    public void Unfreeze()
    {
        IsFrozen = false;
    }

    public void SetReleaseAfter(DateTimeOffset releaseAfter)
    {
        ReleaseAfter = releaseAfter;
    }

    public Result Release(DateTimeOffset now)
    {
        if (Status != DepositStatus.Held)
            return Result.Failure(Error.Conflict("deposit.invalid_state", $"Only held deposits can be released. Current status: {Status}."));

        if (IsFrozen)
            return Result.Failure(PaymentsErrors.Frozen);

        Status = DepositStatus.Released;
        SettledAt = now;
        return Result.Success();
    }

    public Result Deduct(Money amount, DateTimeOffset now)
    {
        if (Status != DepositStatus.Held)
            return Result.Failure(Error.Conflict("deposit.invalid_state", $"Only held deposits can be deducted. Current status: {Status}."));

        if (amount == null || amount.Amount <= 0)
            return Result.Failure(PaymentsErrors.InvalidDeduction);

        if (amount.Amount > Amount.Amount)
            return Result.Failure(PaymentsErrors.DeductionExceedsAmount);

        Status = DepositStatus.Deducted;
        DeductedAmount = amount;
        SettledAt = now;
        return Result.Success();
    }

    public Result Forfeit(DateTimeOffset now)
    {
        if (Status != DepositStatus.Held)
            return Result.Failure(Error.Conflict("deposit.invalid_state", $"Only held deposits can be forfeited. Current status: {Status}."));

        Status = DepositStatus.Forfeited;
        DeductedAmount = Amount;
        SettledAt = now;
        return Result.Success();
    }

    public Result SettleByRuling(int depositToCustomerPercent, DateTimeOffset now)
    {
        if (depositToCustomerPercent < 0 || depositToCustomerPercent > 100)
            return Result.Failure(PaymentsErrors.InvalidPercent);

        if (Status != DepositStatus.Held)
            return Result.Failure(Error.Conflict("deposit.invalid_state", $"Only held deposits can be settled. Current status: {Status}."));

        IsFrozen = false;

        if (depositToCustomerPercent == 100)
        {
            return Release(now);
        }

        if (depositToCustomerPercent == 0)
        {
            return Forfeit(now);
        }

        // Remaining percent is deducted and retained for owner/platform compensation
        var ownerPercent = 100 - depositToCustomerPercent;
        var deduction = Amount.Percent(ownerPercent);
        return Deduct(deduction, now);
    }
}
