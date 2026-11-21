using CarRental.Modules.Payments.Contracts.Enums;
using CarRental.Modules.Payments.Entities;
using Microsoft.EntityFrameworkCore;

namespace CarRental.Modules.Payments.Data.Repositories;

internal sealed class PaymentsRepository(PaymentsDbContext db) : IPaymentsRepository
{
    public Task<Payment?> GetPaymentByIdAsync(PaymentId id, CancellationToken ct = default) =>
        db.Payments.SingleOrDefaultAsync(p => p.Id == id, ct);

    public Task<Payment?> GetLatestPaymentByBookingIdAsync(Guid bookingId, CancellationToken ct = default) =>
        db.Payments
            .Where(p => p.BookingId == bookingId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<Payment?> GetPendingPaymentByBookingIdAsync(Guid bookingId, CancellationToken ct = default) =>
        db.Payments
            .Where(p => p.BookingId == bookingId && p.Status == PaymentStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<Deposit?> GetDepositByIdAsync(DepositId id, CancellationToken ct = default) =>
        db.Deposits.SingleOrDefaultAsync(d => d.Id == id, ct);

    public Task<Deposit?> GetDepositByBookingIdAsync(Guid bookingId, CancellationToken ct = default) =>
        db.Deposits.FirstOrDefaultAsync(d => d.BookingId == bookingId, ct);

    public async Task<IReadOnlyList<Deposit>> GetDueDepositsForReleaseAsync(
        DateTimeOffset now,
        CancellationToken ct = default) =>
        await db.Deposits
            .Where(d => d.Status == DepositStatus.Held
                        && !d.IsFrozen
                        && d.ReleaseAfter != null
                        && d.ReleaseAfter <= now)
            .ToListAsync(ct);

    public Task<bool> HasWebhookBeenProcessedAsync(string transactionId, CancellationToken ct = default) =>
        db.WebhookInboxes.AnyAsync(w => w.Id == transactionId, ct);

    public void AddPayment(Payment payment) =>
        db.Payments.Add(payment);

    public void AddDeposit(Deposit deposit) =>
        db.Deposits.Add(deposit);

    public void AddWebhookInbox(WebhookInbox entry) =>
        db.WebhookInboxes.Add(entry);
}
