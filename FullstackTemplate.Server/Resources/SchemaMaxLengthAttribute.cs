namespace FullstackTemplate.Server.Resources;

using System.Reflection;

/// <summary>
/// Gives the OpenAPI schema of a string field the longest length of a domain rule: the public static int
/// <paramref name="member"/> of <paramref name="owner"/>, for example <c>User.FirstNameMaxLength</c>. A client form reads
/// it, so it checks the same limit with no copy of the number. It is not a request rule: the domain rule still refuses
/// a longer value with its 422 and its message.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SchemaMaxLengthAttribute(Type owner, string member) : Attribute
{
    /// <exception cref="InvalidOperationException">The member is not a public static int field.</exception>
    public int MaxLength =>
        owner.GetField(member, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as int?
        ?? throw new InvalidOperationException($"{owner.Name}.{member} is not a public static int field.");
}
