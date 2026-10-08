namespace SupportDesk.Domain.Exceptions;

/// <summary>
/// Thrown by an aggregate when a requested change would break one of its rules, for example
/// reopening a closed ticket. Surfaces as HTTP 409.
/// </summary>
public sealed class BusinessRuleViolationException : Exception
{
    public BusinessRuleViolationException(string message) : base(message)
    {
    }
}
