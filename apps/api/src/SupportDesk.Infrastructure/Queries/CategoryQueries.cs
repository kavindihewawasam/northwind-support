using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Categories;
using SupportDesk.Domain.Aggregates.Categories;
using SupportDesk.Infrastructure.Data;

namespace SupportDesk.Infrastructure.Queries;

public sealed class CategoryQueries(SupportDbContext db) : ICategoryQueries
{
    public async Task<IReadOnlyList<CategoryListItemDto>> GetAllAsync(CancellationToken ct) =>
        await db.Set<Category>()
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryListItemDto(
                c.Id,
                c.Name,
                c.IsActive,
                c.RequiresSpecialist,
                c.ForcesCriticalPriority))
            .ToListAsync(ct);
}
