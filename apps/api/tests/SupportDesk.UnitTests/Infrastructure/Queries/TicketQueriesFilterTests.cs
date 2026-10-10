using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Contracts.Common;
using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Domain.Aggregates.Customers;
using SupportDesk.Domain.Aggregates.Tickets;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.UnitTests.TestDoubles;
using Xunit;

namespace SupportDesk.UnitTests.Infrastructure.Queries;

/// <summary>
/// Runs the real ticket list query against an in-memory SQLite database, so the tests prove
/// that filtering, counting and paging happen in SQL and not in C#.
/// </summary>
/// <remarks>
/// SQLite's string matching is case-sensitive, unlike SQL Server's default collation, so the
/// search tests use the same casing as the data.
/// </remarks>
public sealed class TicketQueriesFilterTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly SupportDbContext _db;
    private readonly TicketQueries _queries;
    private readonly int _contosoId;
    private readonly int _fabrikamId;

    public TicketQueriesFilterTests()
    {
        // An in-memory SQLite database lives only as long as its connection is open.
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SupportDbContext>().UseSqlite(_connection).Options;
        _db = new SupportDbContext(options);
        _db.Database.EnsureCreated();

        var contoso = new Customer("Contoso Ltd", "it@contoso.test", null, CustomerTier.Premium, FixedClock.DefaultNow);
        var fabrikam = new Customer("Fabrikam Inc", "it@fabrikam.test", null, CustomerTier.Standard, FixedClock.DefaultNow);
        var general = new Category("General", requiresSpecialist: false, forcesCriticalPriority: false);

        _db.AddRange(contoso, fabrikam, general);
        _db.SaveChanges();

        _contosoId = contoso.Id;
        _fabrikamId = fabrikam.Id;

        _db.AddRange(
            NewTicket("TCK-0001", "Invoice total is wrong", contoso.Id, general.Id, TicketPriority.High, TicketStatus.New),
            NewTicket("TCK-0002", "Cannot log in", contoso.Id, general.Id, TicketPriority.High, TicketStatus.InProgress),
            NewTicket("TCK-0003", "Export fails", fabrikam.Id, general.Id, TicketPriority.High, TicketStatus.InProgress),
            NewTicket("TCK-0004", "Slow dashboard", fabrikam.Id, general.Id, TicketPriority.Low, TicketStatus.InProgress),
            NewTicket("TCK-0005", "Printer question", fabrikam.Id, general.Id, TicketPriority.Medium, TicketStatus.Open));
        _db.SaveChanges();

        _queries = new TicketQueries(_db, new FixedClock());
    }

    [Fact]
    public async Task No_filters_returns_every_ticket()
    {
        var result = await Search(new TicketQuery());

        Assert.Equal(5, result.TotalCount);
    }

    [Fact]
    public async Task Priority_is_an_exact_match()
    {
        var result = await Search(new TicketQuery { Priority = TicketPriority.High });

        Assert.Equal(["TCK-0001", "TCK-0002", "TCK-0003"], References(result));
        Assert.Equal(3, result.TotalCount);
    }

    [Fact]
    public async Task Filters_combine_with_and()
    {
        var statusAndPriority = await Search(new TicketQuery
        {
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High
        });

        Assert.Equal(["TCK-0002", "TCK-0003"], References(statusAndPriority));

        var withCustomer = await Search(new TicketQuery
        {
            Status = TicketStatus.InProgress,
            Priority = TicketPriority.High,
            CustomerId = _fabrikamId
        });

        Assert.Equal(["TCK-0003"], References(withCustomer));
        Assert.Equal(1, withCustomer.TotalCount);
    }

    [Fact]
    public async Task Search_matches_the_customer_name()
    {
        var result = await Search(new TicketQuery { Search = "Contoso" });

        Assert.Equal(["TCK-0001", "TCK-0002"], References(result));
        Assert.All(result.Items, item => Assert.Equal(_contosoId, item.Customer.Id));
    }

    [Fact]
    public async Task Search_matches_the_reference_and_the_title()
    {
        var byReference = await Search(new TicketQuery { Search = "TCK-0004" });
        var byTitle = await Search(new TicketQuery { Search = "Printer" });

        Assert.Equal(["TCK-0004"], References(byReference));
        Assert.Equal(["TCK-0005"], References(byTitle));
    }

    [Fact]
    public async Task Search_is_trimmed_and_a_blank_search_is_ignored()
    {
        var padded = await Search(new TicketQuery { Search = "   Contoso   " });
        var blank = await Search(new TicketQuery { Search = "   " });

        Assert.Equal(["TCK-0001", "TCK-0002"], References(padded));
        Assert.Equal(5, blank.TotalCount);
    }

    [Fact]
    public async Task Count_and_pages_describe_the_filtered_set_not_the_whole_table()
    {
        var result = await Search(new TicketQuery { Status = TicketStatus.InProgress, PageSize = 2 });

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(3, result.TotalCount);
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

    private static Ticket NewTicket(
        string reference,
        string title,
        int customerId,
        int categoryId,
        TicketPriority priority,
        TicketStatus status)
    {
        var ticket = Ticket.Raise(
            reference,
            title,
            "A description long enough to be realistic.",
            customerId,
            categoryId,
            priority,
            FixedClock.DefaultNow);

        if (status != TicketStatus.New)
        {
            ticket.ChangeStatus(status, FixedClock.DefaultNow);
        }

        return ticket;
    }
}