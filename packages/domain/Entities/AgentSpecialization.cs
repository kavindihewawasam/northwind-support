namespace SupportDesk.Domain.Entities;

/// <summary>
/// Join entity: the categories an agent is qualified to handle.
/// </summary>
public class AgentSpecialization
{
    public int AgentId { get; set; }

    public Agent Agent { get; set; } = null!;

    public int CategoryId { get; set; }

    public Category Category { get; set; } = null!;
}
