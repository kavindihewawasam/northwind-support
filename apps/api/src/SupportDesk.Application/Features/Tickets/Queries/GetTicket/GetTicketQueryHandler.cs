using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Tickets.Queries.GetTicket;

/// <summary>
/// Returns one ticket with its description and the customer's contact details.
/// </summary>
public sealed class GetTicketQueryHandler(ITicketQueries tickets)
{
    /// <exception cref="NotFoundException">No ticket has this id.</exception>
    public async Task<TicketDetailDto> HandleAsync(int id, CancellationToken ct) =>
        await tickets.GetDetailAsync(id, ct) ?? throw new NotFoundException("Ticket", id);
}
