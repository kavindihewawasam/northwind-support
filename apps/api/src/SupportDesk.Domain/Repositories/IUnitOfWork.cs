namespace SupportDesk.Domain.Repositories;

/// <summary>
/// Commits every change made to the aggregates loaded in the current request, atomically.
/// </summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct);
}
