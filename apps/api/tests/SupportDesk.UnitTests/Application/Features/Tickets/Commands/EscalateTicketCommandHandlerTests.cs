using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

public class EscalateTicketCommandHandlerTests
{
    private static EscalateTicketRequest Request() => new("Customer is blocked", "alex.turner");

    private static AgentCandidate Agent(int id, int open, int max = 10, bool active = true) =>
        new(id, $"Agent {id}", active, max, open, new HashSet<int>());

    private static Ticket Open(TicketPriority priority, int? agentId) =>
        new TicketBuilder().WithPriority(priority).AssignedTo(agentId).Build();

    [Fact]
    public async Task An_unknown_ticket_is_not_found()
    {
        var context = new TicketCommandTestContext();
        context.Tickets.Setup(t => t.GetByIdAsync(99, It.IsAny<CancellationToken>())).ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<NotFoundException>(
            () => context.EscalateTicket.HandleAsync(99, Request(), CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task A_critical_ticket_is_rejected_and_the_message_says_why()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(Open(TicketPriority.Critical, agentId: 4));

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains("Critical", exception.Message);
        Assert.Empty(ticket.Escalations);
        context.VerifySaved(Times.Never());
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public async Task A_resolved_or_closed_ticket_is_rejected_and_the_message_says_which(TicketStatus status)
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(status).Build());

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None));

        Assert.Contains(status.ToString(), exception.Message);
        Assert.Empty(ticket.Escalations);
        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task Escalating_raises_the_priority_restarts_the_window_and_writes_the_history_row()
    {
        var context = new TicketCommandTestContext();
        context.WithAgents(Agent(4, open: 5), Agent(2, open: 1));
        var ticket = context.ExistingTicket(Open(TicketPriority.Medium, agentId: 4));

        var result = await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(context.Clock.UtcNow.AddHours(8), ticket.DueAtUtc);

        var row = Assert.Single(ticket.Escalations);
        Assert.Equal(TicketPriority.Medium, row.FromPriority);
        Assert.Equal(TicketPriority.High, row.ToPriority);
        Assert.Equal("Customer is blocked", row.Reason);
        Assert.Equal("alex.turner", row.EscalatedBy);

        Assert.NotNull(result.Ticket);
        Assert.NotNull(result.Escalation);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task The_current_agent_is_kept_while_still_eligible()
    {
        var context = new TicketCommandTestContext();
        context.WithAgents(Agent(4, open: 5), Agent(2, open: 1));
        var ticket = context.ExistingTicket(Open(TicketPriority.Medium, agentId: 4));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId);
    }

    [Fact]
    public async Task The_ticket_is_reassigned_when_the_current_agent_is_no_longer_eligible()
    {
        var context = new TicketCommandTestContext();
        context.WithAgents(Agent(4, open: 0, active: false), Agent(2, open: 3), Agent(3, open: 2));
        var ticket = context.ExistingTicket(Open(TicketPriority.Medium, agentId: 4));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        Assert.Equal(3, ticket.AssignedAgentId);
        Assert.Equal(4, Assert.Single(ticket.Escalations).FromAgentId);
        Assert.Equal(3, Assert.Single(ticket.Escalations).ToAgentId);
    }

    [Fact]
    public async Task The_tickets_own_load_is_left_out_when_agents_are_loaded()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(Open(TicketPriority.Low, agentId: 4));

        await context.EscalateTicket.HandleAsync(ticket.Id, Request(), CancellationToken.None);

        context.TriageInputs.Verify(
            t => t.GetAgentCandidatesAsync(ticket.Id, It.IsAny<CancellationToken>()), Times.Once());
    }
}