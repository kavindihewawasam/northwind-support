using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The facts the triage rules need that live in the database: the category's handling flags,
/// whether a customer is Premium, and each agent's current workload. Read-only.
/// </summary>
public interface ITriageInputs
{
    /// <returns>Null when the category does not exist.</returns>
    Task<CategoryRules?> GetCategoryRulesAsync(int categoryId, CancellationToken ct);

    /// <returns>Null when the customer does not exist.</returns>
    Task<bool?> IsPremiumCustomerAsync(int customerId, CancellationToken ct);

    /// <param name="excludingTicketId">
    /// A ticket whose own load is left out of its agent's open-ticket count, so an agent at their
    /// limit still counts as eligible for a ticket they already hold (used when escalating).
    /// </param>
    Task<IReadOnlyList<AgentCandidate>> GetAgentCandidatesAsync(int? excludingTicketId, CancellationToken ct);
}