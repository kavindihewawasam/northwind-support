using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Exceptions;
using SupportDesk.Domain.Repositories;

namespace SupportDesk.Application.Features.Tickets.Commands.ChangeTicketStatus;

/// <summary>
/// Moves a ticket through its lifecycle. The rules live on the <c>Ticket</c> aggregate.
/// </summary>
public sealed class ChangeTicketStatusCommandHandler(
    ITicketRepository tickets,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket)
{
    /// <exception cref="NotFoundException">No ticket has this id.</exception>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public async Task<TicketDetailDto> HandleAsync(int id, UpdateTicketStatusRequest request, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        ticket.ChangeStatus(request.Status, clock.UtcNow);

        await unitOfWork.SaveChangesAsync(ct);

        return await getTicket.HandleAsync(id, ct);
    }
}
