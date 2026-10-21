using System.Text.Json;
using CarRental.BuildingBlocks.Domain;
using CarRental.BuildingBlocks.Domain.ValueObjects;

namespace CarRental.Modules.Bookings.Entities;

public class CancellationPolicySnapshot : Entity<Guid>
{
    public Guid BookingId { get; private set; }
    public string PolicyDetailsJson { get; private set; } = string.Empty;

    private CancellationPolicySnapshot() { }

    public CancellationPolicySnapshot(Guid bookingId, string policyDetailsJson)
    {
        Id = bookingId;
        BookingId = bookingId;
        PolicyDetailsJson = string.IsNullOrWhiteSpace(policyDetailsJson) ? "[]" : policyDetailsJson;
    }

    public CancellationOutcome Calculate(Money rentAmount, Money depositAmount, TimeSpan timeUntilStart)
    {
        decimal penaltyPercent = 100m;

        try
        {
            if (!string.IsNullOrWhiteSpace(PolicyDetailsJson) && PolicyDetailsJson != "[]")
            {
                var tiers = JsonSerializer.Deserialize<List<PolicyTier>>(PolicyDetailsJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tiers is { Count: > 0 })
                {
                    // Sort descending by hours threshold
                    var matchedTier = tiers
                        .OrderByDescending(t => t.HoursBeforeStart)
                        .FirstOrDefault(t => timeUntilStart.TotalHours >= t.HoursBeforeStart);

                    if (matchedTier != null)
                    {
                        penaltyPercent = matchedTier.PenaltyPercent;
                    }
                }
            }
            else
            {
                // Default fallback: >= 48h: 0%, >= 24h: 30%, < 24h: 100%
                if (timeUntilStart.TotalHours >= 48)
                {
                    penaltyPercent = 0m;
                }
                else if (timeUntilStart.TotalHours >= 24)
                {
                    penaltyPercent = 30m;
                }
                else
                {
                    penaltyPercent = 100m;
                }
            }
        }
        catch
        {
            penaltyPercent = timeUntilStart.TotalHours >= 24 ? 30m : 100m;
        }

        penaltyPercent = Math.Clamp(penaltyPercent, 0m, 100m);
        var penalty = rentAmount.Percent(penaltyPercent);
        var refundableRent = rentAmount.Subtract(penalty);
        var totalRefundable = refundableRent.Add(depositAmount);

        return new CancellationOutcome(penalty, totalRefundable);
    }

    private sealed record PolicyTier(int HoursBeforeStart, decimal PenaltyPercent);
}
