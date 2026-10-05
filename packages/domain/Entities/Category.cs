namespace SupportDesk.Domain.Entities;

/// <summary>
/// The kind of problem a ticket is about (Billing, Outage, ...). Categories carry the
/// handling rules for the tickets filed against them, so that those rules live in data
/// rather than in code.
/// </summary>
public class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Tickets in this category may only be handled by an agent who lists it as a
    /// specialization.
    /// </summary>
    public bool RequiresSpecialist { get; set; }

    /// <summary>
    /// Tickets in this category are always treated as the most urgent, whatever the
    /// reporter asked for.
    /// </summary>
    public bool ForcesCriticalPriority { get; set; }

    public ICollection<AgentSpecialization> Specialists { get; set; } = new List<AgentSpecialization>();

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
