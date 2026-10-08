namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// Where a ticket stands against its SLA due date. Always derived from the ticket's dates -
/// never stored.
/// </summary>
public enum SlaStatus
{
    /// <summary>The ticket has no due date, so there is nothing to measure against.</summary>
    NotApplicable = 0,

    /// <summary>Still open and comfortably inside the window.</summary>
    WithinSla = 1,

    /// <summary>Still open, inside the window, but close to breaching it.</summary>
    AtRisk = 2,

    /// <summary>The due date has passed without a resolution, or the resolution was late.</summary>
    Breached = 3,

    /// <summary>Resolved or closed on or before the due date.</summary>
    Met = 4
}
