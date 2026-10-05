using SupportDesk.Domain.Enums;

namespace SupportDesk.Domain.Entities;

/// <summary>
/// An organisation that raises support tickets.
/// </summary>
public class Customer
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? Phone { get; set; }

    public CustomerTier Tier { get; set; } = CustomerTier.Standard;

    public DateTime CreatedAtUtc { get; set; }

    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
