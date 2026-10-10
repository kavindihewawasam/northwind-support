using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Exceptions;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

public class TicketEscalationTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow.AddHours(5);

    private static Ticket Open(TicketPriority priority = TicketPriority.Medium, int? agentId = 4) =>
        new TicketBuilder().WithPriority(priority).AssignedTo(agentId).Build();

    private static TicketEscalation Escalate(Ticket ticket, TicketPriority to, int? toAgentId = 2) =>
        ticket.Escalate("alex.turner", "Customer is blocked", to, Now.AddHours(8), TimeSpan.FromHours(8), toAgentId, Now);

    [Fact]
    public void Escalating_raises_the_priority_restarts_the_window_and_changes_the_owner()
    {
        var ticket = Open(TicketPriority.Medium, agentId: 4);

        Escalate(ticket, TicketPriority.High, toAgentId: 2);

        Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(Now.AddHours(8), ticket.DueAtUtc);
        Assert.Equal(480, ticket.SlaWindowMinutes);
        Assert.Equal(2, ticket.AssignedAgentId);
        Assert.Equal(Now, ticket.UpdatedAtUtc);
    }

    [Fact]
    public void Escalating_writes_one_history_row_with_the_before_and_after_values()
    {
        var ticket = Open(TicketPriority.Medium, agentId: 4);

        var escalation = ticket.Escalate(
            "  alex.turner  ", "  Customer is blocked  ", TicketPriority.High, Now.AddHours(8), TimeSpan.FromHours(8), 2, Now);

        Assert.Same(escalation, Assert.Single(ticket.Escalations));
        Assert.Equal(TicketPriority.Medium, escalation.FromPriority);
        Assert.Equal(TicketPriority.High, escalation.ToPriority);
        Assert.Equal(4, escalation.FromAgentId);
        Assert.Equal(2, escalation.ToAgentId);
        Assert.Null(escalation.FromDueAtUtc);
        Assert.Equal(Now.AddHours(8), escalation.ToDueAtUtc);
        Assert.Equal("Customer is blocked", escalation.Reason);
        Assert.Equal("alex.turner", escalation.EscalatedBy);
        Assert.Equal(Now, escalation.EscalatedAtUtc);
    }

    [Fact]
    public void A_ticket_can_be_escalated_more_than_once_and_each_step_is_recorded()
    {
        var ticket = Open(TicketPriority.Low);

        Escalate(ticket, TicketPriority.Medium);
        Escalate(ticket, TicketPriority.High);

        Assert.Equal(2, ticket.Escalations.Count);
        Assert.Equal(TicketPriority.High, ticket.Priority);
    }

    [Fact]
    public void The_owner_can_be_removed_when_nobody_is_eligible()
    {
        var ticket = Open(agentId: 4);

        var escalation = Escalate(ticket, TicketPriority.High, toAgentId: null);

        Assert.Null(ticket.AssignedAgentId);
        Assert.Null(escalation.ToAgentId);
    }

    [Fact]
    public void A_critical_ticket_cannot_be_escalated()
    {
        var ticket = Open(TicketPriority.Critical);

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket, TicketPriority.Critical));

        Assert.Contains("Critical", exception.Message);
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void A_resolved_or_closed_ticket_cannot_be_escalated(TicketStatus status)
    {
        var ticket = new TicketBuilder().WithPriority(TicketPriority.Medium).WithStatus(status).Build();

        var exception = Assert.Throws<BusinessRuleViolationException>(() => Escalate(ticket, TicketPriority.High));

        Assert.Contains("Resolved or closed", exception.Message);
        Assert.Equal(TicketPriority.Medium, ticket.Priority);
        Assert.Empty(ticket.Escalations);
    }

    [Fact]
    public void An_escalation_must_be_exactly_one_level()
    {
        var ticket = Open(TicketPriority.Low);

        Assert.Throws<ArgumentException>(() => Escalate(ticket, TicketPriority.High));
        Assert.Empty(ticket.Escalations);
    }

    [Theory]
    [InlineData("", "Customer is blocked")]
    [InlineData("   ", "Customer is blocked")]
    [InlineData("alex.turner", "")]
    [InlineData("alex.turner", "   ")]
    public void The_actor_and_the_reason_are_required(string escalatedBy, string reason)
    {
        var ticket = Open();

        Assert.Throws<ArgumentException>(() => ticket.Escalate(
            escalatedBy, reason, TicketPriority.High, Now.AddHours(8), TimeSpan.FromHours(8), 2, Now));
    }
}