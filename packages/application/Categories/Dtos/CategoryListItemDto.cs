namespace SupportDesk.Application.Categories.Dtos;

/// <summary>
/// A ticket category and the handling rules recorded against it.
/// </summary>
public sealed record CategoryListItemDto(
    int Id,
    string Name,
    bool IsActive,
    bool RequiresSpecialist,
    bool ForcesCriticalPriority);
