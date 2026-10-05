using SupportDesk.Application.Abstractions;

namespace SupportDesk.UnitTests.TestDoubles;

/// <summary>
/// A clock that does not move unless a test moves it. Anything that depends on "now" should
/// be tested through this, never through the machine clock.
/// </summary>
public sealed class FixedClock(DateTime utcNow) : IClock
{
    public static readonly DateTime DefaultNow = new(2026, 3, 2, 9, 0, 0, DateTimeKind.Utc);

    public FixedClock() : this(DefaultNow)
    {
    }

    public DateTime UtcNow { get; private set; } = utcNow;

    /// <summary>Moves the clock forward, for tests that need time to pass.</summary>
    public void Advance(TimeSpan amount) => UtcNow = UtcNow.Add(amount);
}
