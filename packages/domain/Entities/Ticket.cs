using SupportDesk.Domain.Enums;

namespace SupportDesk.Domain.Entities;

/// <summary>
/// A support request raised by a customer.
/// </summary>
public class Ticket
{
    public int Id { get; set; }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; set; }

    public Agent? AssignedAgent { get; set; }

    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    public TicketStatus Status { get; set; } = TicketStatus.New;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);
}
