namespace FullstackTemplate.Bff.Tests.Fakes;

using Microsoft.Extensions.Options;

/// <summary>
/// An options monitor with one value that never changes.
/// </summary>
public sealed class FixedOptionsMonitor<T>(T value) : IOptionsMonitor<T>
{
    public T CurrentValue => value;

    public T Get(string? name) => value;

    public IDisposable? OnChange(Action<T, string?> listener) => null;
}
