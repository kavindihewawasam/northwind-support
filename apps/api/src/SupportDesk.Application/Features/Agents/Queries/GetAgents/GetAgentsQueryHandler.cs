using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Agents;

namespace SupportDesk.Application.Features.Agents.Queries.GetAgents;

/// <summary>
/// Lists agents with their current workload and the categories they are qualified for.
/// </summary>
public sealed class GetAgentsQueryHandler(IAgentQueries agents)
{
    public Task<IReadOnlyList<AgentDto>> HandleAsync(CancellationToken ct) => agents.GetAllAsync(ct);
}
