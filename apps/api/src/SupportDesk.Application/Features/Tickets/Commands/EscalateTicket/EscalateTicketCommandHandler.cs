using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Application.Features.Tickets.Queries.GetTicket;
using SupportDesk.Application.Features.Tickets.Queries.GetTicketEscalations;
using SupportDesk.Domain.Repositories;
using SupportDesk.Domain.Triage;

namespace SupportDesk.Application.Features.Tickets.Commands.EscalateTicket;

/// <summary>
/// Escalates a ticket: one priority level up, a fresh SLA window and a re-evaluated owner. The
/// decisions are made by <see cref="TicketTriage"/>, the same code that triages a new ticket.
/// </summary>
public sealed class EscalateTicketCommandHandler(
    ITicketRepository tickets,
    ITriageInputs triageInputs,
    TicketTriage triage,
    IUnitOfWork unitOfWork,
    IClock clock,
    GetTicketQueryHandler getTicket,
    GetTicketEscalationsQueryHandler getEscalations,
    ILogger<EscalateTicketCommandHandler> logger)
{
    private const int EscalatedByMaxLength = 100;

    /// <param name="escalatedBy">The signed-in agent, taken from the token by the caller.</param>
    /// <exception cref="NotFoundException">The ticket does not exist.</exception>
    /// <exception cref="ConflictException">The ticket is Critical, resolved or closed.</exception>
    public async Task<EscalationResultDto> HandleAsync(
        int id, EscalateTicketRequest request, string escalatedBy, CancellationToken ct)
    {
        var ticket = await tickets.GetByIdAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

        if (!ticket.IsOpen)
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} is {ticket.Status}; resolved or closed tickets cannot be escalated.");
        }

        if (!PriorityRules.CanEscalate(ticket.Priority))
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} is already Critical; critical tickets cannot be escalated.");
        }

        var category = await triageInputs.GetCategoryRulesAsync(ticket.CategoryId, ct)
            ?? throw new NotFoundException("Category", ticket.CategoryId);

        var isPremium = await triageInputs.IsPremiumCustomerAsync(ticket.CustomerId, ct)
            ?? throw new NotFoundException("Customer", ticket.CustomerId);

        // The ticket's own load is left out, so its current agent is judged as if it were not theirs yet.
        var agents = await triageInputs.GetAgentCandidatesAsync(ticket.Id, ct);

        var now = clock.UtcNow;

        var decision = triage.ForEscalation(
            ticket.Priority,
            ticket.AssignedAgentId,
            new TriageContext(category, isPremium, now, agents));

        var actor = escalatedBy.Trim();

        if (actor.Length > EscalatedByMaxLength)
        {
            actor = actor[..EscalatedByMaxLength];
        }

        ticket.Escalate(
            actor,
            request.Reason,
            decision.Priority,
            decision.DueAtUtc,
            decision.SlaWindow,
            decision.AssignedAgentId,
            now);

        await unitOfWork.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} escalated to {Priority} by {EscalatedBy}.",
            ticket.Reference, decision.Priority, actor);

        var updated = await getTicket.HandleAsync(id, ct);
        var history = await getEscalations.HandleAsync(id, ct);

        // History is newest first, so the first row is the escalation that was just recorded.
        return new EscalationResultDto(updated, history[0]);
    }
}