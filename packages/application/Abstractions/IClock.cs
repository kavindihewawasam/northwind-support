namespace SupportDesk.Application.Abstractions;

/// <summary>
/// The current time, as an injectable dependency.
/// </summary>
/// <remarks>
/// Application and domain code must take the time from here instead of calling
/// <see cref="DateTime.UtcNow"/>, so that behaviour which depends on "now" can be tested
/// without depending on the wall clock.
/// </remarks>
public interface IClock
{
    DateTime UtcNow { get; }
}
