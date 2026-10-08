using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Customers;
using SupportDesk.Application.Exceptions;

namespace SupportDesk.Application.Features.Customers.Queries.GetCustomer;

/// <summary>
/// Returns one customer with their tickets.
/// </summary>
public sealed class GetCustomerQueryHandler(ICustomerQueries customers)
{
    /// <exception cref="NotFoundException">No customer has this id.</exception>
    public async Task<CustomerDetailDto> HandleAsync(int id, CancellationToken ct) =>
        await customers.GetDetailAsync(id, ct) ?? throw new NotFoundException("Customer", id);
}
