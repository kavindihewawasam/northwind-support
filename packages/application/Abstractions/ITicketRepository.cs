using SupportDesk.Application.Common;
using SupportDesk.Application.Tickets.Dtos;
using SupportDesk.Application.Tickets.Queries;
using SupportDesk.Domain.Entities;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// Ticket data access. Read methods return DTOs so that queries can be projected in the
/// database; the single write method hands back a tracked entity to change.
/// </summary>
public interface ITicketRepository
{
    Task<PagedResult<TicketListItemDto>> GetPagedAsync(TicketQuery query, CancellationToken ct);

    Task<TicketDetailDto?> GetDetailAsync(int id, CancellationToken ct);

    /// <summary>Loads a tracked ticket for modification, or null when it does not exist.</summary>
    Task<Ticket?> GetForUpdateAsync(int id, CancellationToken ct);

    Task<IReadOnlyList<TicketListItemDto>> GetForCustomerAsync(int customerId, CancellationToken ct);

    /// <summary>The next unused ticket reference, e.g. TCK-0041.</summary>
    Task<string> NextReferenceAsync(CancellationToken ct);

    Task AddAsync(Ticket ticket, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
