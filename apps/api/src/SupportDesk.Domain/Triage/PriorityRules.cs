using SupportDesk.Domain.Aggregates.Tickets;

namespace SupportDesk.Domain.Triage;

public readonly record struct PriorityDecision(TicketPriority Priority, string Reason);

/// <summary>Which priority a ticket gets when it is raised, and when it is escalated.</summary>
public static class PriorityRules
{
    /// <summary>The requested priority (Medium when none), unless the category forces Critical.</summary>
    public static PriorityDecision Initial(TicketPriority? requested, CategoryRules category)
    {
        if (category.ForcesCriticalPriority)
        {
            return new PriorityDecision(
                TicketPriority.Critical,
                $"{category.Name} tickets are always Critical, whatever was requested.");
        }

        return requested is { } priority
            ? new PriorityDecision(priority, $"Requested priority {priority} was used.")
            : new PriorityDecision(TicketPriority.Medium, "No priority was requested, so Medium was used.");
    }

    public static bool CanEscalate(TicketPriority current) => current < TicketPriority.Critical;

    /// <summary>One level up.</summary>
    /// <exception cref="InvalidOperationException">The ticket is already Critical.</exception>
    public static PriorityDecision Escalate(TicketPriority current)
    {
        if (!CanEscalate(current))
        {
            throw new InvalidOperationException("Critical tickets cannot be escalated.");
        }

        var next = current + 1;

        return new PriorityDecision(next, $"Escalated from {current} to {next}.");
    }
}