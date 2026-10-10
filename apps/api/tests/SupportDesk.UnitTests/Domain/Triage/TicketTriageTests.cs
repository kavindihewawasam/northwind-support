using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Triage;

public class TicketTriageTests
{
    private static readonly CategoryRules General = new(1, "General", RequiresSpecialist: false, ForcesCriticalPriority: false);
    private static readonly CategoryRules Billing = new(2, "Billing", RequiresSpecialist: true, ForcesCriticalPriority: false);
    private static readonly CategoryRules Security = new(3, "Security", RequiresSpecialist: true, ForcesCriticalPriority: true);
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private static SlaPolicy Policy() => new()
    {
        BaseWindowHours =
        {
            [TicketPriority.Low] = 72,
            [TicketPriority.Medium] = 24,
            [TicketPriority.High] = 8,
            [TicketPriority.Critical] = 4
        },
        PremiumCustomerMultiplier = 0.5,
        MinimumWindowHours = 1,
        AtRiskThresholdPercent = 25
    };

    private static AgentCandidate Agent(
        int id, int open, int max = 10, bool active = true, IEnumerable<int>? categories = null) =>
        new(id, $"Agent {id}", active, max, open, (categories ?? []).ToHashSet());

    private static TriageContext Context(CategoryRules category, bool premium = false, params AgentCandidate[] agents) =>
        new(category, premium, Now, agents);

    private static TicketTriage Triage(SlaPolicy? policy = null) => new(policy ?? Policy());

    // ---- BR-1 priority ----

    [Fact]
    public void The_requested_priority_is_used()
    {
        var decision = Triage().ForNewTicket(TicketPriority.High, Context(General));

        Assert.Equal(TicketPriority.High, decision.Priority);
    }

    [Fact]
    public void Medium_is_the_default_priority()
    {
        var decision = Triage().ForNewTicket(null, Context(General));

        Assert.Equal(TicketPriority.Medium, decision.Priority);
    }

    [Fact]
    public void A_category_that_forces_critical_overrides_the_requested_priority()
    {
        var decision = Triage().ForNewTicket(TicketPriority.Low, Context(Security));

        Assert.Equal(TicketPriority.Critical, decision.Priority);
        Assert.Contains("Security", decision.PriorityReason);
    }

    // ---- BR-2 / BR-3 SLA window ----

    [Theory]
    [InlineData(TicketPriority.Low, 72)]
    [InlineData(TicketPriority.Medium, 24)]
    [InlineData(TicketPriority.High, 8)]
    [InlineData(TicketPriority.Critical, 4)]
    public void A_standard_customer_gets_the_base_window(TicketPriority priority, double hours)
    {
        var decision = Triage().ForNewTicket(priority, Context(General));

        Assert.Equal(TimeSpan.FromHours(hours), decision.SlaWindow);
        Assert.Equal(Now.AddHours(hours), decision.DueAtUtc);
    }

    [Theory]
    [InlineData(TicketPriority.Low, 36)]
    [InlineData(TicketPriority.Medium, 12)]
    [InlineData(TicketPriority.High, 4)]
    [InlineData(TicketPriority.Critical, 2)]
    public void A_premium_customer_gets_half_the_window(TicketPriority priority, double hours)
    {
        var decision = Triage().ForNewTicket(priority, Context(General, premium: true));

        Assert.Equal(TimeSpan.FromHours(hours), decision.SlaWindow);
    }

    [Fact]
    public void A_premium_window_is_never_shorter_than_the_floor()
    {
        var policy = Policy();
        policy.BaseWindowHours[TicketPriority.Critical] = 1.5; // half would be 45 minutes

        var decision = Triage(policy).ForNewTicket(TicketPriority.Critical, Context(General, premium: true));

        Assert.Equal(TimeSpan.FromHours(1), decision.SlaWindow);
    }

    [Fact]
    public void A_missing_window_in_the_configuration_is_reported_clearly()
    {
        var policy = Policy();
        policy.BaseWindowHours.Remove(TicketPriority.High);

        Assert.Throws<InvalidOperationException>(
            () => Triage(policy).ForNewTicket(TicketPriority.High, Context(General)));
    }

    // ---- BR-4 / BR-5 assignment ----

    [Fact]
    public void The_agent_with_the_fewest_open_tickets_is_chosen()
    {
        var decision = Triage().ForNewTicket(
            null, Context(General, false, Agent(1, open: 5), Agent(2, open: 3), Agent(3, open: 4)));

        Assert.Equal(2, decision.AssignedAgentId);
    }

    [Fact]
    public void Inactive_agents_are_skipped()
    {
        var decision = Triage().ForNewTicket(
            null, Context(General, false, Agent(1, open: 0, active: false), Agent(2, open: 5)));

        Assert.Equal(2, decision.AssignedAgentId);
    }

    [Fact]
    public void An_agent_must_be_strictly_below_their_limit()
    {
        var decision = Triage().ForNewTicket(
            null, Context(General, false, Agent(1, open: 6, max: 6), Agent(2, open: 5, max: 6)));

        Assert.Equal(2, decision.AssignedAgentId);
    }

    [Fact]
    public void A_specialist_category_only_goes_to_a_specialist()
    {
        var decision = Triage().ForNewTicket(
            null, Context(Billing, false, Agent(1, open: 0), Agent(2, open: 4, categories: [2])));

        Assert.Equal(2, decision.AssignedAgentId);
    }

    [Fact]
    public void A_category_without_the_specialist_flag_ignores_specializations()
    {
        var decision = Triage().ForNewTicket(null, Context(General, false, Agent(1, open: 1)));

        Assert.Equal(1, decision.AssignedAgentId);
    }

    [Fact]
    public void A_tie_goes_to_the_lowest_agent_id()
    {
        var decision = Triage().ForNewTicket(
            null, Context(General, false, Agent(5, open: 2), Agent(3, open: 2), Agent(9, open: 2)));

        Assert.Equal(3, decision.AssignedAgentId);
    }

    [Fact]
    public void When_nobody_is_eligible_the_ticket_is_unassigned_with_a_reason_but_still_triaged()
    {
        var decision = Triage().ForNewTicket(
            TicketPriority.High, Context(Billing, false, Agent(1, open: 0), Agent(2, open: 6, max: 6, categories: [2])));

        Assert.Null(decision.AssignedAgentId);
        Assert.Contains("Billing", decision.AssignmentReason);
        Assert.Equal(TicketPriority.High, decision.Priority);
        Assert.Equal(Now.AddHours(8), decision.DueAtUtc);
    }

    // ---- BR-7 escalation ----

    [Theory]
    [InlineData(TicketPriority.Low, TicketPriority.Medium)]
    [InlineData(TicketPriority.Medium, TicketPriority.High)]
    [InlineData(TicketPriority.High, TicketPriority.Critical)]
    public void Escalation_raises_the_priority_one_level(TicketPriority from, TicketPriority to)
    {
        var decision = Triage().ForEscalation(from, currentAgentId: null, Context(General));

        Assert.Equal(to, decision.Priority);
    }

    [Fact]
    public void Escalation_recomputes_the_due_date_from_now_using_the_new_priority()
    {
        var decision = Triage().ForEscalation(TicketPriority.Medium, currentAgentId: null, Context(General));

        Assert.Equal(Now.AddHours(8), decision.DueAtUtc);
    }

    [Fact]
    public void A_critical_ticket_cannot_be_escalated()
    {
        Assert.Throws<InvalidOperationException>(
            () => Triage().ForEscalation(TicketPriority.Critical, currentAgentId: 1, Context(General)));
    }

    [Fact]
    public void Escalation_keeps_the_current_agent_while_they_are_still_eligible()
    {
        var decision = Triage().ForEscalation(
            TicketPriority.Medium, currentAgentId: 4, Context(General, false, Agent(4, open: 5), Agent(2, open: 1)));

        Assert.Equal(4, decision.AssignedAgentId);
    }

    [Fact]
    public void Escalation_reassigns_when_the_current_agent_is_no_longer_eligible()
    {
        var decision = Triage().ForEscalation(
            TicketPriority.Medium,
            currentAgentId: 4,
            Context(General, false, Agent(4, open: 0, active: false), Agent(2, open: 3), Agent(3, open: 2)));

        Assert.Equal(3, decision.AssignedAgentId);
    }

    [Fact]
    public void Escalation_reassigns_when_the_current_agent_lacks_the_specialization()
    {
        var decision = Triage().ForEscalation(
            TicketPriority.Low,
            currentAgentId: 4,
            Context(Billing, false, Agent(4, open: 0), Agent(2, open: 3, categories: [2])));

        Assert.Equal(2, decision.AssignedAgentId);
    }
}