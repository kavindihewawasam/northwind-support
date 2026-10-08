using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Application.Exceptions;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Application.Features.Tickets.Commands;

/// <remarks>
/// The lifecycle rules themselves are covered by
/// <see cref="Domain.Aggregates.Tickets.TicketTests"/>; these tests
/// cover what the use case adds around them.
/// </remarks>
public class ChangeTicketStatusCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_AppliesTheStatusAtTheCurrentTimeAndSaves()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(TicketStatus.InProgress).Build());

        context.Clock.Advance(TimeSpan.FromHours(3));

        await context.ChangeTicketStatus.HandleAsync(
            ticket.Id, new UpdateTicketStatusRequest(TicketStatus.Resolved), CancellationToken.None);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(context.Clock.UtcNow, ticket.ResolvedAtUtc);
        context.VerifySaved(Times.Once());
    }

    [Fact]
    public async Task HandleAsync_WhenTheTicketDoesNotExist_Throws()
    {
        var context = new TicketCommandTestContext();

        await Assert.ThrowsAsync<NotFoundException>(() => context.ChangeTicketStatus.HandleAsync(
            404, new UpdateTicketStatusRequest(TicketStatus.Open), CancellationToken.None));

        context.VerifySaved(Times.Never());
    }

    [Fact]
    public async Task HandleAsync_WhenTheTicketIsClosed_DoesNotSave()
    {
        var context = new TicketCommandTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(TicketStatus.Closed).Build());

        await Assert.ThrowsAsync<BusinessRuleViolationException>(() => context.ChangeTicketStatus.HandleAsync(
            ticket.Id, new UpdateTicketStatusRequest(TicketStatus.Open), CancellationToken.None));

        context.VerifySaved(Times.Never());
    }
}
