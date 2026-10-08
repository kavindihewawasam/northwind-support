namespace SupportDesk.Domain.Common;

/// <summary>
/// The entry point to a cluster of objects that change together. Repositories load and save
/// whole aggregates, and other aggregates refer to one only by its id.
/// </summary>
public abstract class AggregateRoot : Entity;
