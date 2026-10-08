using SupportDesk.Domain.Common;

namespace SupportDesk.Domain.Aggregates.Customers;

/// <summary>
/// An organisation that raises support tickets.
/// </summary>
public sealed class Customer : AggregateRoot
{
    private Customer()
    {
        // For EF Core.
    }

    public Customer(string name, string email, string? phone, CustomerTier tier, DateTime createdAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        Name = name;
        Email = email;
        Phone = phone;
        Tier = tier;
        CreatedAtUtc = createdAtUtc;
    }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Phone { get; private set; }

    /// <summary>Commercial tier. Premium customers have tighter response expectations.</summary>
    public CustomerTier Tier { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}
