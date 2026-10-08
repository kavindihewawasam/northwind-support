namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// How urgent a ticket is. The numeric values are ordered, lowest urgency first, and are
/// persisted as integers - do not renumber them.
/// </summary>
public enum TicketPriority
{
    Low = 1,
    Medium = 2,
    High = 3,
    Critical = 4
}
