using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Agents.Dtos;

namespace SupportDesk.Application.Agents.Services;

/// <summary>
/// Read-only agent use cases.
/// </summary>
public sealed class AgentService(IAgentRepository agents)
{
    public Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct) => agents.GetAllAsync(ct);
}
