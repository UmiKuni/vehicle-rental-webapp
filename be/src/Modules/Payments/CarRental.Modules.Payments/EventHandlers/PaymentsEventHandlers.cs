using CarRental.Modules.Bookings.Contracts.Events;
using CarRental.Modules.Handover.Contracts.Events;
using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Data;
using CarRental.Modules.Payments.Data.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Attributes;

namespace CarRental.Modules.Payments.EventHandlers;

[WolverineHandler]
public sealed class PaymentsEventHandlers
{
    public Task Handle(BookingCancelled message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IPaymentsRepository>(),
            services.GetRequiredService<PaymentsDbContext>(),
            services.GetRequiredService<TimeProvider>(),
            ct);

    public Task Handle(HandoverConfirmed message, IServiceProvider services, CancellationToken ct = default) =>
        Handle(
            message,
            services.GetRequiredService<IPaymentsRepository>(),
            services.GetRequiredService<PaymentsDbContext>(),
            services.GetRequiredService<TimeProvider>(),
            ct);

    internal async Task Handle(
        BookingCancelled message,
        IPaymentsRepository repository,
        PaymentsDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (message.BookingId == Guid.Empty)
            return;

        var now = clock.GetUtcNow();
        var changed = false;

        var pendingPayment = await repository.GetPendingPaymentByBookingIdAsync(message.BookingId, ct);
        if (pendingPayment is not null)
        {
            var cancelResult = pendingPayment.Cancel(message.Reason, now);
            if (cancelResult.IsSuccess)
                changed = true;
        }

        var deposit = await repository.GetDepositByBookingIdAsync(message.BookingId, ct);
        if (deposit is not null && deposit.Status == DepositStatus.Held)
        {
            var releaseResult = deposit.Release(now);
            if (releaseResult.IsSuccess)
                changed = true;
        }

        if (changed)
            await dbContext.SaveChangesAsync(ct);
    }

    internal async Task Handle(
        HandoverConfirmed message,
        IPaymentsRepository repository,
        PaymentsDbContext dbContext,
        TimeProvider clock,
        CancellationToken ct)
    {
        if (message.BookingId == Guid.Empty)
            return;

        if (!string.Equals(message.Type, "Return", StringComparison.OrdinalIgnoreCase))
            return;

        var deposit = await repository.GetDepositByBookingIdAsync(message.BookingId, ct);
        if (deposit is null || deposit.Status != DepositStatus.Held)
            return;

        var now = clock.GetUtcNow();
        deposit.SetReleaseAfter(now.AddHours(48));
        await dbContext.SaveChangesAsync(ct);
    }
}
