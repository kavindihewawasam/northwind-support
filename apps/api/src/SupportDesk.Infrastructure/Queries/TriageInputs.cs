using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Aggregates.Agents;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

/// <summary>Reads what the triage rules need, in one narrow query each. Nothing is tracked.</summary>
public sealed class TriageInputs(SupportDbContext db) : ITriageInputs
{
    public Task<CategoryRules?> GetCategoryRulesAsync(int categoryId, CancellationToken ct) =>
        db.Set<Category>()
            .AsNoTracking()
            .Where(c => c.Id == categoryId)
            .Select(c => new CategoryRules(c.Id, c.Name, c.RequiresSpecialist, c.ForcesCriticalPriority))
            .FirstOrDefaultAsync(ct);

    public async Task<bool?> IsPremiumCustomerAsync(int customerId, CancellationToken ct)
    {
        var tier = await db.Set<Customer>()
            .AsNoTracking()
            .Where(c => c.Id == customerId)
            .Select(c => (CustomerTier?)c.Tier)
            .FirstOrDefaultAsync(ct);

        return tier is null ? null : tier == CustomerTier.Premium;
    }

    public async Task<IReadOnlyList<AgentCandidate>> GetAgentCandidatesAsync(int? excludingTicketId, CancellationToken ct)
    {
        // Ids start at 1, so 0 never matches a real ticket.
        var excluded = excludingTicketId ?? 0;

        var rows = await db.Set<Agent>()
            .AsNoTracking()
            .Select(a => new
            {
                a.Id,
                a.FullName,
                a.IsActive,
                a.MaxOpenTickets,
                OpenTickets = db.Set<Ticket>().Count(t =>
                    t.AssignedAgentId == a.Id
                    && t.Status != TicketStatus.Resolved
                    && t.Status != TicketStatus.Closed
                    && t.Id != excluded),
                CategoryIds = a.Specializations.Select(s => s.CategoryId).ToList()
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new AgentCandidate(
                r.Id, r.FullName, r.IsActive, r.MaxOpenTickets, r.OpenTickets, r.CategoryIds.ToHashSet()))
            .ToList();
    }
}