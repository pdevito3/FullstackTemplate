namespace FullstackTemplate.Bff.Tests.Fakes;

/// <summary>
/// A clock that moves only when a test moves it.
/// </summary>
public sealed class TestClock(DateTimeOffset now) : TimeProvider
{
    public TestClock() : this(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero))
    {
    }

    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;

    public void Advance(TimeSpan time) => Now += time;
}
