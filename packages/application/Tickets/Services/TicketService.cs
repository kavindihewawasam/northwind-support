using Microsoft.Extensions.Logging;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Common;
using SupportDesk.Application.Common.Exceptions;
using SupportDesk.Application.Tickets.Dtos;
using SupportDesk.Application.Tickets.Queries;
using SupportDesk.Domain.Entities;
using SupportDesk.Domain.Enums;

namespace SupportDesk.Application.Tickets.Services;

/// <summary>
/// The ticket use cases the support team has today: search, read, raise, move through the
/// workflow, and hand to an agent.
/// </summary>
public sealed class TicketService(
    ITicketRepository tickets,
    ICustomerRepository customers,
    ICategoryRepository categories,
    IAgentRepository agents,
    IClock clock,
    ILogger<TicketService> logger)
{
    public Task<PagedResult<TicketListItemDto>> SearchAsync(TicketQuery query, CancellationToken ct) =>
        tickets.GetPagedAsync(query.Normalized(), ct);

    public async Task<TicketDetailDto> GetAsync(int id, CancellationToken ct) =>
        await tickets.GetDetailAsync(id, ct) ?? throw new NotFoundException("Ticket", id);

    /// <summary>
    /// Raises a ticket for a customer.
    /// </summary>
    public async Task<TicketDetailDto> CreateAsync(CreateTicketRequest request, CancellationToken ct)
    {
        if (!await customers.ExistsAsync(request.CustomerId, ct))
        {
            throw new NotFoundException("Customer", request.CustomerId);
        }

        if (!await categories.ExistsAsync(request.CategoryId, ct))
        {
            throw new NotFoundException("Category", request.CategoryId);
        }

        var now = clock.UtcNow;

        var ticket = new Ticket
        {
            Reference = await tickets.NextReferenceAsync(ct),
            Title = request.Title.Trim(),
            Description = request.Description.Trim(),
            CustomerId = request.CustomerId,
            CategoryId = request.CategoryId,
            Priority = request.RequestedPriority ?? TicketPriority.Medium,
            Status = TicketStatus.New,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,

            // The response window is still worked out by hand by the team lead, so a new
            // ticket starts with no due date and no owner.
            DueAtUtc = null,
            AssignedAgentId = null
        };

        await tickets.AddAsync(ticket, ct);
        await tickets.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} raised for customer {CustomerId} in category {CategoryId}.",
            ticket.Reference, ticket.CustomerId, ticket.CategoryId);

        return await ReloadAsync(ticket.Id, ct);
    }

    /// <summary>
    /// Moves a ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    public async Task<TicketDetailDto> ChangeStatusAsync(int id, TicketStatus status, CancellationToken ct)
    {
        var ticket = await tickets.GetForUpdateAsync(id, ct)
                     ?? throw new NotFoundException("Ticket", id);

        if (ticket.Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new ConflictException(
                $"Ticket {ticket.Reference} is closed and cannot be moved to {status}.");
        }

        var now = clock.UtcNow;

        ticket.ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => now,
            TicketStatus.Closed => ticket.ResolvedAtUtc ?? now,
            _ => null
        };

        ticket.Status = status;
        ticket.UpdatedAtUtc = now;

        await tickets.SaveChangesAsync(ct);

        return await ReloadAsync(id, ct);
    }

    /// <summary>
    /// Hands a ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null.
    /// </summary>
    /// <remarks>
    /// This is the manual override the team lead uses, so it deliberately does not check the
    /// category's specialist rule.
    /// </remarks>
    public async Task<TicketDetailDto> AssignAsync(int id, int? agentId, CancellationToken ct)
    {
        var ticket = await tickets.GetForUpdateAsync(id, ct)
                     ?? throw new NotFoundException("Ticket", id);

        if (agentId is not null)
        {
            var agent = await agents.GetByIdAsync(agentId.Value, ct)
                        ?? throw new NotFoundException("Agent", agentId.Value);

            if (!agent.IsActive)
            {
                throw new ConflictException(
                    $"Agent {agent.FullName} is not active and cannot take new tickets.");
            }
        }

        ticket.AssignedAgentId = agentId;
        ticket.UpdatedAtUtc = clock.UtcNow;

        await tickets.SaveChangesAsync(ct);

        logger.LogInformation(
            "Ticket {Reference} assigned to agent {AgentId}.", ticket.Reference, agentId);

        return await ReloadAsync(id, ct);
    }

    private async Task<TicketDetailDto> ReloadAsync(int id, CancellationToken ct) =>
        await tickets.GetDetailAsync(id, ct) ?? throw new NotFoundException("Ticket", id);
}
