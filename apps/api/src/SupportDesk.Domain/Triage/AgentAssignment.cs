namespace SupportDesk.Domain.Triage;

/// <summary>An agent as the assignment rules see them: no database, just the facts that matter.</summary>
public sealed record AgentCandidate(
    int AgentId,
    string FullName,
    bool IsActive,
    int MaxOpenTickets,
    int OpenTickets,
    IReadOnlySet<int> SpecializationCategoryIds);

public readonly record struct AssignmentDecision(int? AgentId, string Reason);

/// <summary>Which agent should own a ticket.</summary>
public static class AgentAssignment
{
    /// <summary>Active, specialised where the category requires it, and strictly below their limit.</summary>
    public static bool IsEligible(AgentCandidate agent, CategoryRules category) =>
        agent.IsActive
        && agent.OpenTickets < agent.MaxOpenTickets
        && (!category.RequiresSpecialist || agent.SpecializationCategoryIds.Contains(category.CategoryId));

    /// <summary>The eligible agent with the fewest open tickets; the lowest id wins a tie.</summary>
    public static AssignmentDecision Choose(IEnumerable<AgentCandidate> agents, CategoryRules category)
    {
        var chosen = agents
            .Where(agent => IsEligible(agent, category))
            .OrderBy(agent => agent.OpenTickets)
            .ThenBy(agent => agent.AgentId)
            .FirstOrDefault();

        if (chosen is null)
        {
            var who = category.RequiresSpecialist
                ? $"No active {category.Name} specialist is below their open-ticket limit"
                : "No active agent is below their open-ticket limit";

            return new AssignmentDecision(null, $"{who}, so the ticket is unassigned.");
        }

        return new AssignmentDecision(
            chosen.AgentId,
            $"Assigned to {chosen.FullName}: fewest open tickets ({chosen.OpenTickets}) among eligible agents.");
    }

    /// <summary>Keeps the current agent while they are still eligible, otherwise chooses again.</summary>
    public static AssignmentDecision KeepOrChoose(
        int? currentAgentId,
        IReadOnlyCollection<AgentCandidate> agents,
        CategoryRules category)
    {
        var current = agents.FirstOrDefault(agent => agent.AgentId == currentAgentId);

        return current is not null && IsEligible(current, category)
            ? new AssignmentDecision(current.AgentId, $"Kept {current.FullName}: still eligible for this ticket.")
            : Choose(agents, category);
    }
}