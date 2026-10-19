namespace FullstackTemplate.Server.Domain;

/// <summary>
/// Makes entity IDs in the form <c>{prefix}_{uuidv7}</c>, for example <c>usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f</c>.
/// The prefix tells a reader the entity type. The UUIDv7 part sorts by creation time to the millisecond.
/// </summary>
public static class EntityId
{
    /// <summary>The column length for an ID and for each foreign key to an ID.</summary>
    public const int MaxLength = 50;

    /// <summary>The column collation for an ID and for each foreign key to an ID. IDs are opaque ASCII, so a byte compare is enough.</summary>
    public const string Collation = "C";

    /// <summary>The longest prefix. The prefix, the <c>_</c>, and the 32 UUID characters fill <see cref="MaxLength"/>.</summary>
    public const int MaxPrefixLength = MaxLength - 1 - 32;

    /// <exception cref="ArgumentException">The prefix does not have 1 to <see cref="MaxPrefixLength"/> lower-case ASCII letters.</exception>
    public static string New(string prefix)
    {
        ThrowIfInvalidPrefix(prefix);
        return $"{prefix}_{Guid.CreateVersion7():N}";
    }

    /// <summary>
    /// Returns true when <paramref name="id"/> starts with <c>{prefix}_</c>, has one or more lower-case ASCII
    /// letters or digits after it, and has no more than <see cref="MaxLength"/> characters. It does not make sure
    /// that the rest is a UUIDv7, so a fixed test ID such as <c>usr_fixed</c> is valid.
    /// </summary>
    /// <exception cref="ArgumentException">The prefix does not have 1 to <see cref="MaxPrefixLength"/> lower-case ASCII letters.</exception>
    public static bool IsValid(string? id, string prefix)
    {
        ThrowIfInvalidPrefix(prefix);
        if (id is null || id.Length <= prefix.Length + 1 || id.Length > MaxLength)
            return false;
        if (!id.StartsWith(prefix, StringComparison.Ordinal) || id[prefix.Length] != '_')
            return false;

        foreach (var c in id.AsSpan(prefix.Length + 1))
        {
            if (!char.IsAsciiLetterLower(c) && !char.IsAsciiDigit(c))
                return false;
        }

        return true;
    }

    private static void ThrowIfInvalidPrefix(string prefix)
    {
        if (prefix is not { Length: > 0 and <= MaxPrefixLength } || !prefix.All(char.IsAsciiLetterLower))
            throw new ArgumentException(
                $"The prefix '{prefix}' must have 1 to {MaxPrefixLength} lower-case ASCII letters.", nameof(prefix));
    }
}
