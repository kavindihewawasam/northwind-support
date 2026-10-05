using SupportDesk.Application.Common.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Tickets;

public class TicketServiceAssignTests
{
    [Fact]
    public async Task AssignAsync_GivesTheTicketToAnActiveAgent()
    {
        var context = new TicketServiceTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().Build());
        context.ExistingAgent(id: 4, fullName: "Sara Lindqvist");

        context.Clock.Advance(TimeSpan.FromMinutes(15));

        await context.Service.AssignAsync(ticket.Id, agentId: 4, CancellationToken.None);

        Assert.Equal(4, ticket.AssignedAgentId);
        Assert.Equal(context.Clock.UtcNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public async Task AssignAsync_WithoutAnAgent_TakesTheTicketBack()
    {
        var context = new TicketServiceTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().AssignedTo(2).Build());

        await context.Service.AssignAsync(ticket.Id, agentId: null, CancellationToken.None);

        Assert.Null(ticket.AssignedAgentId);
        context.Agents.Verify(
            a => a.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task AssignAsync_WhenTheAgentIsInactive_Throws()
    {
        var context = new TicketServiceTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().Build());
        context.ExistingAgent(id: 6, fullName: "Yuki Tanaka", isActive: false);

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.Service.AssignAsync(ticket.Id, agentId: 6, CancellationToken.None));

        Assert.Contains("Yuki Tanaka", exception.Message);
        Assert.Null(ticket.AssignedAgentId);
    }
}
