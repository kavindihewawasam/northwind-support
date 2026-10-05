using SupportDesk.Application.Categories.Dtos;

namespace SupportDesk.Application.Abstractions;

public interface ICategoryRepository
{
    Task<IReadOnlyList<CategoryListItemDto>> GetAllAsync(CancellationToken ct);

    Task<bool> ExistsAsync(int id, CancellationToken ct);
}
