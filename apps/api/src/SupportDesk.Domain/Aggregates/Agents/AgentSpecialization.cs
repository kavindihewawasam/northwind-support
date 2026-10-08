namespace SupportDesk.Domain.Aggregates.Agents;

/// <summary>
/// A category an agent is qualified to handle. Part of the <see cref="Agent"/> aggregate; the
/// category is another aggregate and is referenced by id.
/// </summary>
public sealed class AgentSpecialization
{
    private AgentSpecialization()
    {
        // For EF Core.
    }

    internal AgentSpecialization(int categoryId)
    {
        CategoryId = categoryId;
    }

    public int AgentId { get; private set; }

    public int CategoryId { get; private set; }
}
