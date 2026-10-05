using SupportDesk.Application.Common.Exceptions;
using SupportDesk.Application.Tickets.Dtos;
using SupportDesk.Domain.Enums;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Tickets;

public class TicketServiceCreateTests
{
    private static CreateTicketRequest Request(TicketPriority? priority = null) =>
        new("Cannot log in to the portal", "Happens for every user since this morning.", 1, 3, priority);

    [Fact]
    public async Task CreateAsync_StampsReferenceStatusAndTimestamps()
    {
        var context = new TicketServiceTestContext();

        await context.Service.CreateAsync(Request(TicketPriority.High), CancellationToken.None);

        var created = context.AddedTicket;
        Assert.NotNull(created);
        Assert.Equal("TCK-0041", created.Reference);
        Assert.Equal(TicketStatus.New, created.Status);
        Assert.Equal(TicketPriority.High, created.Priority);
        Assert.Equal(FixedClock.DefaultNow, created.CreatedAtUtc);
        Assert.Equal(FixedClock.DefaultNow, created.UpdatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_WithoutRequestedPriority_FallsBackToMedium()
    {
        var context = new TicketServiceTestContext();

        await context.Service.CreateAsync(Request(), CancellationToken.None);

        Assert.NotNull(context.AddedTicket);
        Assert.Equal(TicketPriority.Medium, context.AddedTicket.Priority);
    }

    [Fact]
    public async Task CreateAsync_WhenCustomerDoesNotExist_Throws()
    {
        var context = new TicketServiceTestContext();
        context.Customers
            .Setup(c => c.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var exception = await Assert.ThrowsAsync<NotFoundException>(
            () => context.Service.CreateAsync(Request(), CancellationToken.None));

        Assert.Contains("Customer", exception.Message);
        context.Tickets.Verify(t => t.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
