using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Domain.Aggregates.Tickets;

/// <summary>SLA status at the exact boundaries (BR-8): a 100-minute window with a 25% threshold.</summary>
public class SlaEvaluatorAtRiskTests
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private static SlaStatus Evaluate(DateTime? due, DateTime? resolved = null) =>
        SlaEvaluator.Evaluate(due, resolved, Now, windowMinutes: 100, atRiskPercent: 25);

    [Fact]
    public void No_due_date_means_not_applicable() =>
        Assert.Equal(SlaStatus.NotApplicable, Evaluate(null));

    [Fact]
    public void Plenty_of_time_left_is_within_sla() =>
        Assert.Equal(SlaStatus.WithinSla, Evaluate(Now.AddMinutes(60)));

    [Fact]
    public void Exactly_25_percent_remaining_is_at_risk() =>
        Assert.Equal(SlaStatus.AtRisk, Evaluate(Now.AddMinutes(25)));

    [Fact]
    public void A_second_more_than_25_percent_remaining_is_within_sla() =>
        Assert.Equal(SlaStatus.WithinSla, Evaluate(Now.AddMinutes(25).AddSeconds(1)));

    [Fact]
    public void Due_exactly_now_is_at_risk_not_breached() =>
        Assert.Equal(SlaStatus.AtRisk, Evaluate(Now));

    [Fact]
    public void Past_due_and_unresolved_is_breached() =>
        Assert.Equal(SlaStatus.Breached, Evaluate(Now.AddTicks(-1)));

    [Fact]
    public void Resolved_exactly_on_the_due_date_is_met() =>
        Assert.Equal(SlaStatus.Met, Evaluate(Now.AddMinutes(10), resolved: Now.AddMinutes(10)));

    [Fact]
    public void Resolved_late_is_breached() =>
        Assert.Equal(SlaStatus.Breached, Evaluate(Now.AddMinutes(-10), resolved: Now.AddMinutes(-9)));

    [Fact]
    public void Resolved_early_is_met_even_if_it_was_close_to_the_deadline() =>
        Assert.Equal(SlaStatus.Met, Evaluate(Now.AddMinutes(5), resolved: Now.AddMinutes(-1)));

    [Fact]
    public void Without_a_window_the_ticket_cannot_be_at_risk() =>
        Assert.Equal(SlaStatus.WithinSla, SlaEvaluator.Evaluate(Now.AddMinutes(1), null, Now));
}