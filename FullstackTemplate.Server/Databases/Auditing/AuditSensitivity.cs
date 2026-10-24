namespace FullstackTemplate.Server.Databases.Auditing;

using System.Reflection;
using Domain;

internal enum Sensitivity
{
    NotSensitive,
    Sensitive,

    /// <summary>A value that needs a mark and has none, or has both marks. The read hides it, and a unit test fails.</summary>
    Unclassified,
}

internal static class AuditSensitivity
{
    /// <summary>The types that need no mark, besides an enum.</summary>
    public static IReadOnlySet<Type> PlainTypes { get; } = new HashSet<Type>
    {
        typeof(bool), typeof(Guid), typeof(sbyte), typeof(byte), typeof(short), typeof(ushort), typeof(int),
        typeof(uint), typeof(long), typeof(ulong),
    };

    /// <summary>
    /// A mark decides. With no mark, a plain value (a flag, an enum, a Guid, or an integer) and a foreign key are not
    /// sensitive. Each other value needs a mark, because a date, a decimal, or text can be personal, for example a
    /// birth date or a salary.
    /// </summary>
    /// <param name="member">The property or field that has the mark. Null for a shadow property.</param>
    /// <param name="valueType">The type of the stored value: the provider type of an entity property, the type of a
    /// complex property, or the type of an action property.</param>
    /// <param name="isForeignKey">True for an entity property that holds the ID of another entity.</param>
    public static Sensitivity Of(MemberInfo? member, Type valueType, bool isForeignKey)
    {
        var sensitive = member?.IsDefined(typeof(SensitiveAttribute)) == true;
        var notSensitive = member?.IsDefined(typeof(NotSensitiveAttribute)) == true;
        return (sensitive, notSensitive) switch
        {
            (true, false) => Sensitivity.Sensitive,
            (false, true) => Sensitivity.NotSensitive,
            (false, false) when isForeignKey || IsPlain(Nullable.GetUnderlyingType(valueType) ?? valueType)
                => Sensitivity.NotSensitive,
            _ => Sensitivity.Unclassified,
        };
    }

    private static bool IsPlain(Type type) => type.IsEnum || PlainTypes.Contains(type);
}
