using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>
/// The SLA settings, bound from the "Sla" configuration section so they can change without
/// touching code. There are deliberately no defaults here: the configuration is the single
/// source of truth.
/// </summary>
public sealed class SlaPolicy
{
    public Dictionary<TicketPriority, double> BaseWindowHours { get; set; } = [];

    /// <summary>Premium customers get this fraction of the base window (0.5 = half).</summary>
    public double PremiumCustomerMultiplier { get; set; }

    /// <summary>A premium window is never shorter than this.</summary>
    public double MinimumWindowHours { get; set; }

    /// <summary>A ticket is at risk when this percent of its window or less remains.</summary>
    public double AtRiskThresholdPercent { get; set; }

    public TimeSpan WindowFor(TicketPriority priority, bool isPremiumCustomer)
    {
        if (!BaseWindowHours.TryGetValue(priority, out var baseHours))
        {
            throw new InvalidOperationException($"No SLA window is configured for {priority} priority.");
        }

        var hours = isPremiumCustomer
            ? Math.Max(baseHours * PremiumCustomerMultiplier, MinimumWindowHours)
            : baseHours;

        return TimeSpan.FromHours(hours);
    }
}