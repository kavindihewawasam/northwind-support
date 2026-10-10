using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Domain.Triage;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// The SLA status filter against a real (SQLite) database. SQLite has no DATEDIFF, so the
/// "at risk" and "within SLA" filters, which need it, are checked by hand against SQL Server;
/// the status rules themselves are covered by SlaEvaluatorAtRiskTests.
/// </summary>
public sealed class TicketQueriesSlaFilterTests : IDisposable
{
    private static readonly DateTime Now = FixedClock.DefaultNow;

    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;

    public TicketQueriesSlaFilterTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options;
        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        var customer = new Customer("Contoso Ltd", "it@contoso.test", null, CustomerTier.Standard, Now);
        var category = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        _db.AddRange(customer, category);
        _db.SaveChanges();

        _db.AddRange(
            NewTicket("TCK-0001", customer.Id, category.Id, due: null, resolvedAt: null),                           // no SLA
            NewTicket("TCK-0002", customer.Id, category.Id, due: Now.AddHours(-1), resolvedAt: null),              // open, past due
            NewTicket("TCK-0003", customer.Id, category.Id, due: Now.AddHours(-1), resolvedAt: Now.AddHours(-2)),  // resolved in time
            NewTicket("TCK-0004", customer.Id, category.Id, due: Now.AddHours(-1), resolvedAt: Now.AddMinutes(-30))); // resolved late
        _db.SaveChanges();

        _queries = new TicketQueries(_db, new FixedClock(), new SlaPolicy { AtRiskThresholdPercent = 25 });
    }

    [Fact]
    public async Task Not_applicable_returns_tickets_without_a_due_date()
    {
        var result = await Search(new TicketQuery { SlaStatus = SlaStatus.NotApplicable });

        Assert.Equal(["TCK-0001"], References(result));
    }

    [Fact]
    public async Task Met_returns_tickets_resolved_on_or_before_the_due_date()
    {
        var result = await Search(new TicketQuery { SlaStatus = SlaStatus.Met });

        Assert.Equal(["TCK-0003"], References(result));
    }

    [Fact]
    public async Task Breached_returns_open_tickets_past_due_and_tickets_resolved_late()
    {
        var result = await Search(new TicketQuery { SlaStatus = SlaStatus.Breached });

        Assert.Equal(["TCK-0002", "TCK-0004"], References(result));
    }

    [Fact]
    public async Task The_sla_filter_combines_with_other_filters_and_the_count_follows()
    {
        var result = await Search(new TicketQuery { SlaStatus = SlaStatus.Breached, Status = TicketStatus.Resolved });

        Assert.Equal(["TCK-0004"], References(result));
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Count_and_pages_describe_the_filtered_set()
    {
        var result = await Search(new TicketQuery { SlaStatus = SlaStatus.Breached, PageSize = 1 });

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(2, result.TotalPages);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private Task<PagedResult<TicketListItemDto>> Search(TicketQuery query) =>
        _queries.GetPagedAsync(query, CancellationToken.None);

    private static string[] References(PagedResult<TicketListItemDto> result) =>
        result.Items.Select(item => item.Reference).Order().ToArray();

    private static Ticket NewTicket(string reference, int customerId, int categoryId, DateTime? due, DateTime? resolvedAt)
    {
        var createdAt = Now.AddHours(-3);

        var ticket = Ticket.Raise(
            reference,
            "Something is not working",
            "A description long enough to be realistic.",
            customerId,
            categoryId,
            TicketPriority.Medium,
            createdAt);

        if (due is { } dueAt)
        {
            ticket.ApplyTriage(TicketPriority.Medium, dueAt, TimeSpan.FromHours(2), null, createdAt);
        }

        if (resolvedAt is { } at)
        {
            ticket.ChangeStatus(TicketStatus.Resolved, at);
        }

        return ticket;
    }
}