using SupportDesk.Domain.Aggregates.Agents;

namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Loads <see cref="Agent"/> aggregates.
/// </summary>
public interface IAgentRepository
{
    Task<Agent?> GetByIdAsync(int id, CancellationToken ct);

    /// <summary>Read-only lookup used to sign in; null when no agent has this email.</summary>
    Task<Agent?> GetByEmailAsync(string email, CancellationToken ct);
}