using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;

namespace SupportDesk.Application.Features.Tickets.Queries.SearchTickets;

/// <summary>
/// Searches tickets with filters, sorting and paging.
/// </summary>
public sealed class SearchTicketsQueryHandler(ITicketQueries tickets)
{
    public Task<PagedResult<TicketListItemDto>> HandleAsync(TicketQuery query, CancellationToken ct) =>
        tickets.GetPagedAsync(query.Normalized(), ct);
}
