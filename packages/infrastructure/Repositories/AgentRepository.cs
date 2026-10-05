using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Agents.Dtos;
using SupportDesk.Application.Common.Dtos;
using SupportDesk.Domain.Entities;
using SupportDesk.Domain.Enums;
using SupportDesk.Infrastructure.Persistence;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class AgentRepository(SupportDbContext db) : IAgentRepository
{
    public async Task<IReadOnlyList<AgentDto>> GetAllAsync(CancellationToken ct) =>
        await Project(db.Agents.AsNoTracking().OrderBy(a => a.FullName)).ToListAsync(ct);

    public Task<AgentDto?> GetByIdAsync(int id, CancellationToken ct) =>
        Project(db.Agents.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync(ct);

    /// <summary>
    /// Adds the current open workload and the specializations to an agent query.
    /// </summary>
    /// <remarks>
    /// Filter and order the <paramref name="agents"/> query before projecting: a projection
    /// that carries a collection cannot be filtered afterwards in SQL.
    /// </remarks>
    private static IQueryable<AgentDto> Project(IQueryable<Agent> agents) =>
        agents.Select(a => new AgentDto(
            a.Id,
            a.FullName,
            a.Email,
            a.IsActive,
            a.MaxOpenTickets,
            a.Tickets.Count(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed),
            a.Specializations
                .Select(s => new CategorySummaryDto(s.CategoryId, s.Category.Name))
                .ToList()));
}
