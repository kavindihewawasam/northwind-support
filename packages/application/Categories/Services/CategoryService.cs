using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Categories.Dtos;

namespace SupportDesk.Application.Categories.Services;

/// <summary>
/// Read-only category use cases.
/// </summary>
public sealed class CategoryService(ICategoryRepository categories)
{
    public Task<IReadOnlyList<CategoryListItemDto>> GetAllAsync(CancellationToken ct) =>
        categories.GetAllAsync(ct);
}
