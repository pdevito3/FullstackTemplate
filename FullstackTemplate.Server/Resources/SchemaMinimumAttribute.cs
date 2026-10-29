namespace FullstackTemplate.Server.Resources;

using System.Reflection;

/// <summary>
/// Gives the OpenAPI schema of a number field the smallest value of a domain rule: the public static int, long, decimal,
/// or double <paramref name="member"/> of <paramref name="owner"/>. A client form reads it, so it checks the same bound
/// with no copy of the number. It is not a request rule: the domain rule still refuses a smaller value with its 422 and
/// its message.
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SchemaMinimumAttribute(Type owner, string member) : Attribute
{
    /// <exception cref="InvalidOperationException">The member is not a public static number field.</exception>
    public decimal Minimum =>
        owner.GetField(member, BindingFlags.Public | BindingFlags.Static)?.GetValue(null) switch
        {
            int value => value,
            long value => value,
            decimal value => value,
            double value => (decimal)value,
            _ => throw new InvalidOperationException(
                $"{owner.Name}.{member} is not a public static int, long, decimal, or double field."),
        };
}
