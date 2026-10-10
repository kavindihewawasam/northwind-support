namespace SupportDesk.Application.Exceptions;

/// <summary>
/// Thrown when someone could not be authenticated, for example a wrong password. Surfaces as HTTP 401.
/// The message is deliberately the same for every cause, so it does not help an attacker.
/// </summary>
public sealed class UnauthorizedException(string message) : Exception(message);