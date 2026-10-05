using SupportDesk.Application.Customers.Dtos;

namespace SupportDesk.Application.Abstractions;

public interface ICustomerRepository
{
    Task<IReadOnlyList<CustomerListItemDto>> GetAllAsync(CancellationToken ct);

    Task<CustomerDetailDto?> GetDetailAsync(int id, CancellationToken ct);

    Task<bool> ExistsAsync(int id, CancellationToken ct);
}
