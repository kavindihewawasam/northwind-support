namespace SupportDesk.Domain.Aggregates.Customers;

/// <summary>
/// Commercial tier of a customer. Premium customers have tighter response expectations.
/// </summary>
public enum CustomerTier
{
    Standard = 1,
    Premium = 2
}
