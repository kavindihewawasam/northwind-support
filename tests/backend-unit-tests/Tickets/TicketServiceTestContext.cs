using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Agents.Dtos;
using SupportDesk.Application.Common.Dtos;
using SupportDesk.Application.Tickets.Dtos;
using SupportDesk.Application.Tickets.Services;
using SupportDesk.Domain.Entities;
using SupportDesk.Domain.Enums;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Tickets;

/// <summary>
/// Shared arrangement for the <see cref="TicketService"/> tests: mocked repositories, a fixed
/// clock, and a service wired to both.
/// </summary>
internal sealed class TicketServiceTestContext
{
    public TicketServiceTestContext()
    {
        Customers.Setup(c => c.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Categories.Setup(c => c.ExistsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Tickets.Setup(t => t.NextReferenceAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("TCK-0041");

        Tickets.Setup(t => t.AddAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .Callback<Ticket, CancellationToken>((ticket, _) => AddedTicket = ticket)
            .Returns(Task.CompletedTask);

        Tickets.Setup(t => t.GetDetailAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketDetailDto
            {
                Id = 41,
                Reference = "TCK-0041",
                Customer = new CustomerContactDto(1, "Contoso Ltd", "support@contoso.example", null, CustomerTier.Premium),
                Category = new CategoryDto(1, "General", false)
            });

        Service = new TicketService(
            Tickets.Object,
            Customers.Object,
            Categories.Object,
            Agents.Object,
            Clock,
            NullLogger<TicketService>.Instance);
    }

    public Mock<ITicketRepository> Tickets { get; } = new();

    public Mock<ICustomerRepository> Customers { get; } = new();

    public Mock<ICategoryRepository> Categories { get; } = new();

    public Mock<IAgentRepository> Agents { get; } = new();

    public FixedClock Clock { get; } = new();

    public TicketService Service { get; }

    /// <summary>The ticket handed to the repository by the last create call.</summary>
    public Ticket? AddedTicket { get; private set; }

    /// <summary>Makes <see cref="ITicketRepository.GetForUpdateAsync"/> return this ticket.</summary>
    public Ticket ExistingTicket(Ticket ticket)
    {
        Tickets.Setup(t => t.GetForUpdateAsync(ticket.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ticket);

        return ticket;
    }

    public void NoSuchTicket(int id) =>
        Tickets.Setup(t => t.GetForUpdateAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket?)null);

    public void ExistingAgent(int id, string fullName = "Alex Turner", bool isActive = true) =>
        Agents.Setup(a => a.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentDto(id, fullName, $"agent{id}@example.com", isActive, 10, 0, []));

}
