namespace SupportDesk.Domain.Common;

/// <summary>
/// Something with an identity that outlives changes to its state. The id is assigned by the
/// database, so it is zero until the entity has been saved.
/// </summary>
public abstract class Entity
{
    public int Id { get; protected set; }
}
