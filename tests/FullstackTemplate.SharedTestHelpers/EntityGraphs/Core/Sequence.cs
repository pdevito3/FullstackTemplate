namespace FullstackTemplate.SharedTestHelpers.EntityGraphs.Core;

/// <summary>Creates reusable sequence definitions; progression lives in each session.</summary>
public static class Sequence
{
    /// <summary>Defines a session-scoped sequence starting at the supplied value.</summary>
    public static Sequence<T> Of<T>(T start, Func<T, T> next)
    {
        ArgumentNullException.ThrowIfNull(next);
        return new(start, next, null);
    }
}

/// <summary>An immutable sequence definition shared by blueprints and their variants.</summary>
/// <remarks>Explicit values are reserved before filling a step. Later steps cannot change earlier assignments.</remarks>
public sealed class Sequence<T>
{
    internal Sequence(T start, Func<T, T> next, Type? scope)
    {
        Start = start;
        Advance = next;
        Scope = scope;
    }

    internal T Start { get; }
    internal Func<T, T> Advance { get; }
    internal Type? Scope { get; }

    /// <summary>Returns a definition scoped to the entire session (the default).</summary>
    public Sequence<T> PerSession() => Scope is null ? this : new(Start, Advance, null);

    /// <summary>Returns a definition scoped to the immediate parent by reference identity.</summary>
    public Sequence<T> PerParent() =>
        Scope == typeof(ParentScope) ? this : new(Start, Advance, typeof(ParentScope));

    /// <summary>Returns a definition scoped to the nearest matching ancestor by reference identity.</summary>
    public Sequence<T> Per<TAncestor>() =>
        Scope == typeof(TAncestor) ? this : new(Start, Advance, typeof(TAncestor));
}

internal sealed class ParentScope;

internal sealed class SequenceState<T>(T start)
{
    internal T Current = start;
    internal HashSet<T> Used { get; } = [];
}
