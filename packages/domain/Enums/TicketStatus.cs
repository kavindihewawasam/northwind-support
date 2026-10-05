namespace SupportDesk.Domain.Enums;

/// <summary>
/// Lifecycle of a ticket. <see cref="Resolved"/> and <see cref="Closed"/> are the two
/// terminal states; everything else counts as open work for an agent.
/// </summary>
public enum TicketStatus
{
    New = 1,
    Open = 2,
    InProgress = 3,
    Resolved = 4,
    Closed = 5
}
