using SupportDesk.Application.Tickets.Dtos;
using SupportDesk.Domain.Enums;

namespace SupportDesk.Application.Customers.Dtos;

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
