using SupportDesk.Application.Contracts.Tickets;
using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Application.Contracts.Customers;

/// <summary>One row of the customer list.</summary>
public sealed record CustomerListItemDto(
    int Id,
    string Name,
    string Email,
    string? Phone,
    CustomerTier Tier,
    int OpenTicketCount);

/// <summary>A customer with their tickets.</summary>
public sealed record CustomerDetailDto(
    int Id,
    string Name,
    string Email,
    string? Phone,
    CustomerTier Tier,
    DateTime CreatedAtUtc,
    IReadOnlyList<TicketListItemDto> Tickets);
