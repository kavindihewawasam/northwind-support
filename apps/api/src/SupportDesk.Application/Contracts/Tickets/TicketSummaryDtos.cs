using SupportDesk.Domain.Aggregates.Customers;

namespace SupportDesk.Application.Contracts.Tickets;

/// <summary>Just enough of a customer to label a ticket.</summary>
public sealed record CustomerSummaryDto(int Id, string Name, CustomerTier Tier);

/// <summary>A customer with the contact details shown on the ticket detail screen.</summary>
public sealed record CustomerContactDto(int Id, string Name, string Email, string? Phone, CustomerTier Tier);

/// <summary>Just enough of a category to label a ticket.</summary>
public sealed record CategorySummaryDto(int Id, string Name);

/// <summary>A category together with the handling rule that matters on a ticket.</summary>
public sealed record CategoryDto(int Id, string Name, bool RequiresSpecialist);

/// <summary>Just enough of an agent to label a ticket.</summary>
public sealed record AgentSummaryDto(int Id, string FullName);
