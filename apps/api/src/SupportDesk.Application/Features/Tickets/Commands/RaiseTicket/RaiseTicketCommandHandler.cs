using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Commands.RaiseTicket;

/// <summary>
/// Raises a ticket for a customer and triages it: priority, due date and owner are decided
/// by <see cref="TicketTriage"/>, the same code that re-triages a ticket when it is escalated.
/// </summary>
public sealed class RaiseTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ITriageInputs triageInputs,
    TicketTriage triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    ILogger<RaiseTicketCommandHandler> logger)
{
    /// <exception cref="NotFoundException">The customer or the category does not exist.</exception>
    public async Task<TicketDetailDto> HandleAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        var category = await triageInputs.GetCategoryRulesAsync(request.CategoryId, ct)
            ?? throw new NotFoundException("Category", request.CategoryId);

        var isPremium = await triageInputs.IsPremiumCustomerAsync(request.CustomerId, ct)
            ?? throw new NotFoundException("Customer", request.CustomerId);

        var agents = await triageInputs.GetAgentCandidatesAsync(excludingTicketId: null, ct);

        var now = clock.UtcNow;

        // Nobody being eligible is a normal outcome: the decision simply has no owner.
        var decision = triage.ForNewTicket(
            request.RequestedPriority,
            new TriageContext(category, isPremium, now, agents));

        var ticket = Ticket.Raise(
            await tickets.NextReferenceAsync(ct),
            request.Title,
            request.Description,
            request.CustomerId,
            request.CategoryId,
            decision.Priority,
            now);

        ticket.ApplyTriage(decision.Priority, decision.DueAtUtc, decision.SlaWindow, decision.AssignedAgentId, now);

        await tickets.AddAsync(ticket, ct);
        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}: {Priority}, agent {AgentId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId, ticket.Priority, ticket.AssignedAgentId);

        var created = await getTicket.HandleAsync(ticket.Id, ct);

        created.Triage = new TriageDto(
            decision.PriorityReason,
            (int)decision.SlaWindow.TotalMinutes,
            decision.SlaReason,
            decision.AssignmentReason);

        return created;
    }
}