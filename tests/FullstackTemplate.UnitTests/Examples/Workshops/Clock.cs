namespace FullstackTemplate.UnitTests.Examples.Workshops;

using Microsoft.Extensions.Time.Testing;

internal static class Clock
{
    public static readonly DateTimeOffset Now = new(2026, 3, 2, 15, 0, 0, TimeSpan.Zero);

    public static TimeProvider Fixed => new FakeTimeProvider(Now);
}
