using SupportDesk.Domain.Entities;
using SupportDesk.Domain.Enums;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// Builds tickets for tests, so each test only has to say what actually matters to it.
/// </summary>
public sealed class TicketBuilder
{
    private int _id = 1;
    private string _reference = "TCK-0001";
    private TicketPriority _priority = TicketPriority.Medium;
    private TicketStatus _status = TicketStatus.New;
    private int? _assignedAgentId;
    private DateTime _createdAtUtc = FixedClock.DefaultNow;
    private DateTime? _dueAtUtc;
    private DateTime? _resolvedAtUtc;

    public TicketBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public TicketBuilder WithReference(string reference)
    {
        _reference = reference;
        return this;
    }

    public TicketBuilder WithPriority(TicketPriority priority)
    {
        _priority = priority;
        return this;
    }

    public TicketBuilder WithStatus(TicketStatus status)
    {
        _status = status;
        return this;
    }

    public TicketBuilder AssignedTo(int? agentId)
    {
        _assignedAgentId = agentId;
        return this;
    }

    public TicketBuilder CreatedAt(DateTime createdAtUtc)
    {
        _createdAtUtc = createdAtUtc;
        return this;
    }

    public TicketBuilder DueAt(DateTime? dueAtUtc)
    {
        _dueAtUtc = dueAtUtc;
        return this;
    }

    public TicketBuilder ResolvedAt(DateTime? resolvedAtUtc)
    {
        _resolvedAtUtc = resolvedAtUtc;
        return this;
    }

    public Ticket Build() => new()
    {
        Id = _id,
        Reference = _reference,
        Title = "Something is not working",
        Description = "A description long enough to be realistic.",
        CustomerId = 1,
        CategoryId = 1,
        AssignedAgentId = _assignedAgentId,
        Priority = _priority,
        Status = _status,
        CreatedAtUtc = _createdAtUtc,
        UpdatedAtUtc = _createdAtUtc,
        DueAtUtc = _dueAtUtc,
        ResolvedAtUtc = _resolvedAtUtc
    };
}
