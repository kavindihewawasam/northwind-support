using SupportDesk.Application.Contracts.Categories;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The read side of categories.
/// </summary>
public interface ICategoryQueries
{
    Task<IReadOnlyList<CategoryListItemDto>> GetAllAsync(CancellationToken ct);
}
