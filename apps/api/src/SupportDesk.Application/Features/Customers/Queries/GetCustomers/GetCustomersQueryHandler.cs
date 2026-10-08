using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Contracts.Customers;

namespace SupportDesk.Application.Features.Customers.Queries.GetCustomers;

/// <summary>
/// Lists customers with how many open tickets each one has.
/// </summary>
public sealed class GetCustomersQueryHandler(ICustomerQueries customers)
{
    public Task<IReadOnlyList<CustomerListItemDto>> HandleAsync(CancellationToken ct) =>
        customers.GetAllAsync(ct);
}
