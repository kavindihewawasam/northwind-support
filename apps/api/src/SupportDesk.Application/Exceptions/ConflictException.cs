namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when a request is understood but conflicts with the current state of the resource,
/// for example reopening a closed ticket. Surfaces as HTTP 409.
/// </summary>
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message)
    {
    }
}
