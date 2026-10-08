namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when a requested resource does not exist. Surfaces as HTTP 404.
/// </summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string resource, object key)
        : base($"{resource} '{key}' was not found.")
    {
    }
}
