using CarRental.Modules.Payments.Entities;

namespace CarRental.Modules.Payments.Data.Repositories;

public interface IPaymentsRepository
{
    Task<Payment?> GetPaymentByIdAsync(PaymentId id, CancellationToken ct = default);

    Task<Payment?> GetLatestPaymentByBookingIdAsync(Guid bookingId, CancellationToken ct = default);

    Task<Payment?> GetPendingPaymentByBookingIdAsync(Guid bookingId, CancellationToken ct = default);

    Task<Deposit?> GetDepositByIdAsync(DepositId id, CancellationToken ct = default);

    Task<Deposit?> GetDepositByBookingIdAsync(Guid bookingId, CancellationToken ct = default);

    Task<IReadOnlyList<Deposit>> GetDueDepositsForReleaseAsync(DateTimeOffset now, CancellationToken ct = default);

    Task<bool> HasWebhookBeenProcessedAsync(string transactionId, CancellationToken ct = default);

    void AddPayment(Payment payment);

    void AddDeposit(Deposit deposit);

    void AddWebhookInbox(WebhookInbox entry);
}
