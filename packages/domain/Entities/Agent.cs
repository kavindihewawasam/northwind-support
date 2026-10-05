namespace SupportDesk.Domain.Entities;

/// <summary>
/// A member of the support team.
/// </summary>
public class Agent
{
    public int Id { get; set; }

    public string FullName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Inactive agents keep their history but take no new work.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>How many open tickets this agent is willing to hold at once.</summary>
    public int MaxOpenTickets { get; set; } = 10;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<AgentSpecialization> Specializations { get; set; } = new List<AgentSpecialization>();

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
