using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Contracts.Agents;

/// <summary>
/// A support agent, with the current workload and the categories they are qualified for.
/// </summary>
public sealed record AgentDto(
    int Id,
    string FullName,
    string Email,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTicketCount,
    IReadOnlyList<CategorySummaryDto> Specializations);
