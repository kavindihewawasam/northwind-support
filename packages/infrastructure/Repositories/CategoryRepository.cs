using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Categories.Dtos;
using SupportDesk.Infrastructure.Persistence;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class CategoryRepository(SupportDbContext db) : ICategoryRepository
{
    public async Task<IReadOnlyList<CategoryListItemDto>> GetAllAsync(CancellationToken ct) =>
        await db.Categories
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CategoryListItemDto(
                c.Id,
                c.Name,
                c.IsActive,
                c.RequiresSpecialist,
                c.ForcesCriticalPriority))
            .ToListAsync(ct);

    public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
        db.Categories.AsNoTracking().AnyAsync(c => c.Id == id, ct);
}
