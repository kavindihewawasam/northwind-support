using SupportDesk.Application.Common.Dtos;
using SupportDesk.Domain.Enums;

namespace SupportDesk.Application.Tickets.Dtos;

/// <summary>
/// One row of the ticket list. Everything the list screen needs, so that it never has to
/// call back per row.
/// </summary>
public sealed class TicketListItemDto
{
    public int Id { get; set; }

    public string Reference { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public TicketStatus Status { get; set; }

    public TicketPriority Priority { get; set; }

    public CustomerSummaryDto Customer { get; set; } = null!;

    public CategorySummaryDto Category { get; set; } = null!;

    public AgentSummaryDto? AssignedAgent { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>Derived from the dates above; never stored.</summary>
    public SlaStatus SlaStatus { get; set; }
}
