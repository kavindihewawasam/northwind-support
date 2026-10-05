using SupportDesk.Application.Agents.Dtos;

namespace SupportDesk.Application.Abstractions;

public interface IAgentRepository
{
    Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct);

    Task<AgentDto?> GetByIdAsync(int id, CancellationToken ct);
}
