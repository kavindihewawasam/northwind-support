using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Common.Exceptions;
using SupportDesk.Application.Customers.Dtos;

namespace SupportDesk.Application.Customers.Services;

/// <summary>
/// Read-only customer use cases.
/// </summary>
public sealed class CustomerService(ICustomerRepository customers)
{
    public Task<IReadOnlyList<CustomerListItemDto>> GetAllAsync(CancellationToken ct) =>
        customers.GetAllAsync(ct);

    public async Task<CustomerDetailDto> GetAsync(int id, CancellationToken ct) =>
        await customers.GetDetailAsync(id, ct)
        ?? throw new NotFoundException("Customer", id);
}
