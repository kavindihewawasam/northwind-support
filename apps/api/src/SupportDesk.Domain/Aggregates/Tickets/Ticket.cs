using SupportDesk.Domain.Common;
using SupportDesk.Domain.Exceptions;

namespace SupportDesk.Domain.Aggregates.Tickets;

/// <summary>
/// A support request raised by a customer. Every change to a ticket goes through one of its
/// methods, so its rules cannot be bypassed.
/// </summary>
/// <remarks>
/// The customer, the category and the assigned agent are other aggregates and are referenced
/// by id only.
/// </remarks>
public sealed class Ticket : AggregateRoot
{
    private readonly List<TicketEscalation> _escalations = [];

    private Ticket()
    {
        // For EF Core.
    }

    /// <summary>Human-friendly identifier shown to customers, e.g. TCK-0042.</summary>
    public string Reference { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int CustomerId { get; private set; }

    public int CategoryId { get; private set; }

    /// <summary>Null while the ticket is waiting for an owner.</summary>
    public int? AssignedAgentId { get; private set; }

    public TicketPriority Priority { get; private set; }

    public TicketStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>When the team has committed to responding by. Null when no SLA applies.</summary>
    public DateTime? DueAtUtc { get; private set; }

    /// <summary>
    /// The length of the current SLA window in minutes, set whenever the due date is set. It is
    /// needed to tell "at risk" from "within SLA", and it restarts when a ticket is escalated.
    /// </summary>
    public int? SlaWindowMinutes { get; private set; }

    public DateTime? ResolvedAtUtc { get; private set; }

    /// <summary>Every escalation of this ticket. Rows are only ever added.</summary>
    public IReadOnlyCollection<TicketEscalation> Escalations => _escalations;

    /// <summary>True while the ticket still needs work from an agent.</summary>
    public bool IsOpen => Status is not (TicketStatus.Resolved or TicketStatus.Closed);

    /// <summary>
    /// Raises a new ticket for a customer. It starts as <see cref="TicketStatus.New"/>, with no
    /// owner and no due date.
    /// </summary>
    public static Ticket Raise(
        string reference,
        string title,
        string description,
        int customerId,
        int categoryId,
        TicketPriority priority,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);

        return new Ticket
        {
            Reference = reference,
            Title = title.Trim(),
            Description = description.Trim(),
            CustomerId = customerId,
            CategoryId = categoryId,
            Priority = priority,
            Status = TicketStatus.New,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    /// <summary>
    /// Records the outcome of triage on a new ticket: its priority, due date and owner (or no
    /// owner when nobody was eligible).
    /// </summary>
    public void ApplyTriage(
        TicketPriority priority,
        DateTime dueAtUtc,
        TimeSpan slaWindow,
        int? assignedAgentId,
        DateTime nowUtc)
    {
        Priority = priority;
        DueAtUtc = dueAtUtc;
        SlaWindowMinutes = ToWholeMinutes(slaWindow);
        AssignedAgentId = assignedAgentId;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Raises the priority one level, restarts the SLA window and changes the owner, and writes
    /// an immutable history row. The new values are decided by the triage rules; this method
    /// guards that the escalation is allowed and that it really is one step up.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is resolved, closed or already Critical.</exception>
    public TicketEscalation Escalate(
        string escalatedBy,
        string reason,
        TicketPriority toPriority,
        DateTime toDueAtUtc,
        TimeSpan slaWindow,
        int? toAgentId,
        DateTime nowUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(escalatedBy);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        if (!IsOpen)
        {
            throw new BusinessRuleViolationException(
                $"Resolved or closed tickets cannot be escalated ({Reference} is {Status}).");
        }

        if (Priority == TicketPriority.Critical)
        {
            throw new BusinessRuleViolationException(
                $"Critical tickets cannot be escalated ({Reference} is already Critical).");
        }

        if (toPriority != Priority + 1)
        {
            throw new ArgumentException("An escalation raises the priority by exactly one level.", nameof(toPriority));
        }

        var escalation = new TicketEscalation(
            Id,
            Priority,
            toPriority,
            AssignedAgentId,
            toAgentId,
            DueAtUtc,
            toDueAtUtc,
            reason.Trim(),
            escalatedBy.Trim(),
            nowUtc);

        _escalations.Add(escalation);

        Priority = toPriority;
        DueAtUtc = toDueAtUtc;
        SlaWindowMinutes = ToWholeMinutes(slaWindow);
        AssignedAgentId = toAgentId;
        UpdatedAtUtc = nowUtc;

        return escalation;
    }

    /// <summary>
    /// Moves the ticket through its lifecycle, keeping the resolution timestamp in step.
    /// </summary>
    /// <exception cref="BusinessRuleViolationException">The ticket is closed.</exception>
    public void ChangeStatus(TicketStatus status, DateTime nowUtc)
    {
        if (Status == TicketStatus.Closed && status != TicketStatus.Closed)
        {
            throw new BusinessRuleViolationException(
                $"Ticket {Reference} is closed and cannot be moved to {status}.");
        }

        ResolvedAtUtc = status switch
        {
            TicketStatus.Resolved => nowUtc,
            TicketStatus.Closed => ResolvedAtUtc ?? nowUtc,
            _ => null
        };

        Status = status;
        UpdatedAtUtc = nowUtc;
    }

    /// <summary>
    /// Hands the ticket to an agent, or takes it back off them when <paramref name="agentId"/>
    /// is null. Whether the agent may take it is checked by the caller, which can see agents.
    /// </summary>
    public void AssignTo(int? agentId, DateTime nowUtc)
    {
        AssignedAgentId = agentId;
        UpdatedAtUtc = nowUtc;
    }

    private static int ToWholeMinutes(TimeSpan window) =>
        (int)Math.Round(window.TotalMinutes, MidpointRounding.AwayFromZero);
}