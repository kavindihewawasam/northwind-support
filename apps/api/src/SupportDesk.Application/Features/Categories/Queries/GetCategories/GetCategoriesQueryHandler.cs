using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Categories;

namespace SupportDesk.Application.Features.Categories.Queries.GetCategories;

/// <summary>
/// Lists the ticket categories and their handling rules.
/// </summary>
public sealed class GetCategoriesQueryHandler(ICategoryQueries categories)
{
    public Task<IReadOnlyList<CategoryListItemDto>> HandleAsync(CancellationToken ct) =>
        categories.GetAllAsync(ct);
}
