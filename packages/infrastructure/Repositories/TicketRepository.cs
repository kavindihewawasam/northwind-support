using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Common;
using SupportDesk.Application.Common.Dtos;
using SupportDesk.Application.Tickets.Dtos;
using SupportDesk.Application.Tickets.Queries;
using SupportDesk.Domain.Entities;
using SupportDesk.Domain.Enums;
using SupportDesk.Infrastructure.Persistence;

namespace SupportDesk.Infrastructure.Repositories;

/// <summary>
/// Ticket queries and persistence. Reads are projected in the database and never tracked;
/// only <see cref="GetForUpdateAsync"/> returns a tracked entity.
/// </summary>
public sealed class TicketRepository(SupportDbContext db, IClock clock) : ITicketRepository
{
    public async Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct)
    {
        var filtered = ApplyFilters(db.Tickets.AsNoTracking(), query);

        var totalCount = await filtered.CountAsync(ct);

        var items = await ApplySort(filtered, query)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToListItem(clock.UtcNow))
            .ToListAsync(ct);

        return new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var now = clock.UtcNow;

        return db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new TicketDetailDto
            {
                Id = t.Id,
                Reference = t.Reference,
                Title = t.Title,
                Description = t.Description,
                Status = t.Status,
                Priority = t.Priority,
                Customer = new CustomerContactDto(
                    t.Customer.Id, t.Customer.Name, t.Customer.Email, t.Customer.Phone, t.Customer.Tier),
                Category = new CategoryDto(t.Category.Id, t.Category.Name, t.Category.RequiresSpecialist),
                AssignedAgent = t.AssignedAgent == null
                    ? null
                    : new AgentSummaryDto(t.AssignedAgent.Id, t.AssignedAgent.FullName),
                CreatedAtUtc = t.CreatedAtUtc,
                UpdatedAtUtc = t.UpdatedAtUtc,
                DueAtUtc = t.DueAtUtc,
                ResolvedAtUtc = t.ResolvedAtUtc,
                SlaStatus =
                    t.DueAtUtc == null ? SlaStatus.NotApplicable
                    : t.ResolvedAtUtc != null
                        ? (t.ResolvedAtUtc <= t.DueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
                    : now > t.DueAtUtc ? SlaStatus.Breached
                    : SlaStatus.WithinSla
            })
            .FirstOrDefaultAsync(ct);
    }

    public Task<Ticket?> GetForUpdateAsync(int id, CancellationToken ct) =>
        db.Tickets.FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct) =>
        await db.Tickets
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId)
            .OrderByDescending(t => t.CreatedAtUtc)
            .ThenByDescending(t => t.Id)
            .Select(ToListItem(clock.UtcNow))
            .ToListAsync(ct);

    public async Task<string> NextReferenceAsync(CancellationToken ct)
    {
        // Single-instance application: the highest id so far is good enough. A database
        // sequence would be the answer if more than one API instance created tickets.
        var lastId = await db.Tickets
            .AsNoTracking()
            .OrderByDescending(t => t.Id)
            .Select(t => t.Id)
            .FirstOrDefaultAsync(ct);

        return $"TCK-{lastId + 1:D4}";
    }

    public async Task AddAsync(Ticket ticket, CancellationToken ct) =>
        await db.Tickets.AddAsync(ticket, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    /// <summary>
    /// The list-row projection, shared by every query that returns list rows.
    /// </summary>
    private static Expression<Func<Ticket, TicketListItemDto>> ToListItem(DateTime now) =>
        t => new TicketListItemDto
        {
            Id = t.Id,
            Reference = t.Reference,
            Title = t.Title,
            Status = t.Status,
            Priority = t.Priority,
            Customer = new CustomerSummaryDto(t.Customer.Id, t.Customer.Name, t.Customer.Tier),
            Category = new CategorySummaryDto(t.Category.Id, t.Category.Name),
            AssignedAgent = t.AssignedAgent == null
                ? null
                : new AgentSummaryDto(t.AssignedAgent.Id, t.AssignedAgent.FullName),
            CreatedAtUtc = t.CreatedAtUtc,
            UpdatedAtUtc = t.UpdatedAtUtc,
            DueAtUtc = t.DueAtUtc,
            ResolvedAtUtc = t.ResolvedAtUtc,
            SlaStatus =
                t.DueAtUtc == null ? SlaStatus.NotApplicable
                : t.ResolvedAtUtc != null
                    ? (t.ResolvedAtUtc <= t.DueAtUtc ? SlaStatus.Met : SlaStatus.Breached)
                : now > t.DueAtUtc ? SlaStatus.Breached
                : SlaStatus.WithinSla
        };

    private static IQueryable<Ticket> ApplyFilters(IQueryable<Ticket> source, TicketQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();

            source = source.Where(t =>
                t.Title.Contains(term) ||
                t.Reference.Contains(term) ||
                t.Customer.Name.Contains(term));
        }

        if (query.Status is not null)
        {
            source = source.Where(t => t.Status == query.Status);
        }

        if (query.Priority is not null)
        {
            // Left over from the "show me everything at least this urgent" experiment.
            source = source.Where(t => t.Priority >= query.Priority);
        }

        if (query.CategoryId is not null)
        {
            source = source.Where(t => t.CategoryId == query.CategoryId);
        }

        if (query.CustomerId is not null)
        {
            source = source.Where(t => t.CustomerId == query.CustomerId);
        }

        if (query.AssignedAgentId is not null)
        {
            source = source.Where(t => t.AssignedAgentId == query.AssignedAgentId);
        }

        if (query.UnassignedOnly)
        {
            source = source.Where(t => t.AssignedAgentId == null);
        }

        return source;
    }

    private static IQueryable<Ticket> ApplySort(IQueryable<Ticket> source, TicketQuery query)
    {
        var descending = !string.Equals(query.SortDirection, "asc", StringComparison.OrdinalIgnoreCase);

        var sorted = query.SortBy?.ToLowerInvariant() switch
        {
            "updatedatutc" => descending
                ? source.OrderByDescending(t => t.UpdatedAtUtc)
                : source.OrderBy(t => t.UpdatedAtUtc),
            "dueatutc" => descending
                ? source.OrderByDescending(t => t.DueAtUtc)
                : source.OrderBy(t => t.DueAtUtc),
            "priority" => descending
                ? source.OrderByDescending(t => t.Priority)
                : source.OrderBy(t => t.Priority),
            "status" => descending
                ? source.OrderByDescending(t => t.Status)
                : source.OrderBy(t => t.Status),
            _ => descending
                ? source.OrderByDescending(t => t.CreatedAtUtc)
                : source.OrderBy(t => t.CreatedAtUtc)
        };

        // Keeps paging stable when the sort column has duplicates.
        return sorted.ThenByDescending(t => t.Id);
    }
}
