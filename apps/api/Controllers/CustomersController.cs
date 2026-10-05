using Microsoft.AspNetCore.Mvc;
using SupportDesk.Application.Customers.Dtos;
using SupportDesk.Application.Customers.Services;

namespace SupportDesk.Api.Controllers;

[ApiController]
[Route("api/customers")]
[Produces("application/json")]
public sealed class CustomersController(CustomerService customers) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<CustomerListItemDto>> GetAll(CancellationToken ct) =>
        customers.GetAllAsync(ct);

    [HttpGet("{id:int}")]
    public Task<CustomerDetailDto> Get(int id, CancellationToken ct) => customers.GetAsync(id, ct);
}
