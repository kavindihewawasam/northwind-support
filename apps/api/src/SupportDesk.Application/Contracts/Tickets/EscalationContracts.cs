using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>Body of POST /api/tickets/{id}/escalate. Who is escalating comes from the token, not the body.</summary>
public sealed record EscalateTicketRequest(string Reason);

/// <summary>One row of a ticket's escalation history.</summary>
public sealed record TicketEscalationDto(
    int Id,
    int TicketId,
    TicketPriority FromPriority,
    TicketPriority ToPriority,
    AgentSummaryDto? FromAgent,
    AgentSummaryDto? ToAgent,
    DateTime? FromDueAtUtc,
    DateTime ToDueAtUtc,
    string Reason,
    string EscalatedBy,
    DateTime EscalatedAtUtc);

/// <summary>The updated ticket together with the escalation that was just recorded.</summary>
public sealed record EscalationResultDto(TicketDetailDto Ticket, TicketEscalationDto Escalation);

/// <summary>Why triage decided what it did, in words. Returned when a ticket is created.</summary>
public sealed record TriageDto(
    string PriorityReason,
    int SlaWindowMinutes,
    string SlaReason,
    string AssignmentReason);