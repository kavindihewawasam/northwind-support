namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Works out where a ticket stands against its due date. SLA status is always derived from
/// the ticket's dates and the current time - never stored.
/// </summary>
public static class SlaEvaluator
{
    /// <param name="dueAtUtc">When the ticket is due; null when no SLA applies.</param>
    /// <param name="resolvedAtUtc">When it was resolved, if it has been.</param>
    /// <param name="nowUtc">The current time, passed in so it can be faked in tests.</param>
    /// <param name="windowMinutes">Length of the current SLA window. Without it "at risk" cannot be told apart.</param>
    /// <param name="atRiskPercent">A ticket is at risk when this percent of the window or less remains.</param>
    public static SlaStatus Evaluate(
        DateTime? dueAtUtc,
        DateTime? resolvedAtUtc,
        DateTime nowUtc,
        int? windowMinutes = null,
        double atRiskPercent = 0)
    {
        if (dueAtUtc is not { } due)
        {
            return SlaStatus.NotApplicable;
        }

        if (resolvedAtUtc is { } resolved)
        {
            return resolved <= due ? SlaStatus.Met : SlaStatus.Breached;
        }

        if (nowUtc > due)
        {
            return SlaStatus.Breached;
        }

        if (windowMinutes is { } window
            && due - nowUtc <= TimeSpan.FromMinutes(window * atRiskPercent / 100))
        {
            return SlaStatus.AtRisk;
        }

        return SlaStatus.WithinSla;
    }
}