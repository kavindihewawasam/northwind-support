using SupportDesk.Application.Contracts.Customers;

namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The read side of customers.
/// </summary>
public interface ICustomerQueries
{
    Task<IReadOnlyList<CustomerListItemDto>> GetAllAsync(CancellationToken ct);

    Task<CustomerDetailDto?> GetDetailAsync(int id, CancellationToken ct);
}
