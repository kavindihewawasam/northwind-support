using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Categories.Dtos;
using SupportDesk.Application.Categories.Services;

namespace SupportDesk.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public sealed class CategoriesController(CategoryService categories) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CategoryListItemDto>> GetAll(CancellationToken ct) =>
        categories.GetAllAsync(ct);
}
