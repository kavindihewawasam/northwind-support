using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

/// <summary>The category facts the rules need.</summary>
public sealed record CategoryRules(int CategoryId, string Name, bool RequiresSpecialist, bool ForcesCriticalPriority);

public sealed record TriageContext(
    CategoryRules Category,
    bool IsPremiumCustomer,
    DateTime NowUtc,
    IReadOnlyCollection<AgentCandidate> Agents);

/// <summary>What the system decided, and why, in words a person can read.</summary>
public sealed record TriageDecision(
    TicketPriority Priority,
    string PriorityReason,
    DateTime DueAtUtc,
    TimeSpan SlaWindow,
    string SlaReason,
    int? AssignedAgentId,
    string AssignmentReason);

/// <summary>
/// Decides priority, due date and owner. One place, used by both creating and escalating a
/// ticket, so the two can never drift apart.
/// </summary>
public sealed class TicketTriage(SlaPolicy sla)
{
    public TriageDecision ForNewTicket(TicketPriority? requested, TriageContext context) =>
        Build(
            PriorityRules.Initial(requested, context.Category),
            AgentAssignment.Choose(context.Agents, context.Category),
            context);

    /// <exception cref="InvalidOperationException">The ticket is already Critical.</exception>
    public TriageDecision ForEscalation(TicketPriority current, int? currentAgentId, TriageContext context) =>
        Build(
            PriorityRules.Escalate(current),
            AgentAssignment.KeepOrChoose(currentAgentId, context.Agents, context.Category),
            context);

    private TriageDecision Build(PriorityDecision priority, AssignmentDecision assignment, TriageContext context)
    {
        var window = sla.WindowFor(priority.Priority, context.IsPremiumCustomer);

        var slaReason = context.IsPremiumCustomer
            ? $"{priority.Priority} priority for a Premium customer: {window.TotalHours:0.##} h window."
            : $"{priority.Priority} priority: {window.TotalHours:0.##} h window.";

        return new TriageDecision(
            priority.Priority,
            priority.Reason,
            context.NowUtc + window,
            window,
            slaReason,
            assignment.AgentId,
            assignment.Reason);
    }
}