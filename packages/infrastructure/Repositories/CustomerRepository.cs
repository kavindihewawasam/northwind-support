using Microsoft.EntityFrameworkCore;
using SupportDesk.Application.Abstractions;
using SupportDesk.Application.Customers.Dtos;
using SupportDesk.Domain.Enums;
using SupportDesk.Infrastructure.Persistence;

namespace SupportDesk.Infrastructure.Repositories;

public sealed class CustomerRepository(SupportDbContext db, ITicketRepository tickets) : ICustomerRepository
{
    public async Task<IReadOnlyList<CustomerListItemDto>> GetAllAsync(CancellationToken ct) =>
        await db.Customers
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CustomerListItemDto(
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.Tier,
                c.Tickets.Count(t => t.Status != TicketStatus.Resolved && t.Status != TicketStatus.Closed)))
            .ToListAsync(ct);

    public async Task<CustomerDetailDto?> GetDetailAsync(int id, CancellationToken ct)
    {
        var customer = await db.Customers
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.Tier,
                c.CreatedAtUtc
            })
            .FirstOrDefaultAsync(ct);

        if (customer is null)
        {
            return null;
        }

        var customerTickets = await tickets.GetForCustomerAsync(id, ct);

        return new CustomerDetailDto(
            customer.Id,
            customer.Name,
            customer.Email,
            customer.Phone,
            customer.Tier,
            customer.CreatedAtUtc,
            customerTickets);
    }

    public Task<bool> ExistsAsync(int id, CancellationToken ct) =>
        db.Customers.AsNoTracking().AnyAsync(c => c.Id == id, ct);
}
