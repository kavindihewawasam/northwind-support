using SupportDesk.Application.Common.Exceptions;
using SupportDesk.Domain.Enums;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Tickets;

public class TicketServiceStatusTests
{
    [Fact]
    public async Task ChangeStatusAsync_WhenResolving_StampsResolutionAndUpdateTimes()
    {
        var context = new TicketServiceTestContext();
        var ticket = context.ExistingTicket(
            new TicketBuilder().WithStatus(TicketStatus.InProgress).Build());

        context.Clock.Advance(TimeSpan.FromHours(3));

        await context.Service.ChangeStatusAsync(ticket.Id, TicketStatus.Resolved, CancellationToken.None);

        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.Equal(context.Clock.UtcNow, ticket.ResolvedAtUtc);
        Assert.Equal(context.Clock.UtcNow, ticket.UpdatedAtUtc);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenReopening_ClearsTheResolutionTime()
    {
        var context = new TicketServiceTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder()
            .WithStatus(TicketStatus.Resolved)
            .ResolvedAt(FixedClock.DefaultNow)
            .Build());

        await context.Service.ChangeStatusAsync(ticket.Id, TicketStatus.InProgress, CancellationToken.None);

        Assert.Equal(TicketStatus.InProgress, ticket.Status);
        Assert.Null(ticket.ResolvedAtUtc);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenClosingAnUnresolvedTicket_RecordsAResolutionTime()
    {
        var context = new TicketServiceTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder().WithStatus(TicketStatus.Open).Build());

        context.Clock.Advance(TimeSpan.FromDays(1));

        await context.Service.ChangeStatusAsync(ticket.Id, TicketStatus.Closed, CancellationToken.None);

        Assert.Equal(context.Clock.UtcNow, ticket.ResolvedAtUtc);
    }

    [Fact]
    public async Task ChangeStatusAsync_WhenTicketIsAlreadyClosed_Throws()
    {
        var context = new TicketServiceTestContext();
        var ticket = context.ExistingTicket(new TicketBuilder()
            .WithReference("TCK-0026")
            .WithStatus(TicketStatus.Closed)
            .Build());

        var exception = await Assert.ThrowsAsync<ConflictException>(
            () => context.Service.ChangeStatusAsync(ticket.Id, TicketStatus.Open, CancellationToken.None));

        Assert.Contains("TCK-0026", exception.Message);
        Assert.Equal(TicketStatus.Closed, ticket.Status);
    }
}
