namespace FullstackTemplate.Server.Databases.Auditing;

using System.Reflection;
using Domain;

internal enum Sensitivity
{
    NotSensitive,
    Sensitive,

    /// <summary>Text or a structure with no mark, or with both marks. The read hides it, and a unit test fails.</summary>
    Unclassified,
}

internal static class AuditSensitivity
{
    /// <summary>
    /// A mark decides. With no mark, a plain value (a number, a flag, a date, a time, a Guid, or an enum) is not
    /// sensitive, because it holds no personal text. Text and a structure need a mark.
    /// </summary>
    /// <param name="member">The property or field that has the mark. Null for a shadow property.</param>
    /// <param name="valueType">The type of the stored value: the provider type of an entity property, the type of a
    /// complex property, or the type of an action property.</param>
    public static Sensitivity Of(MemberInfo? member, Type valueType)
    {
        var sensitive = member?.IsDefined(typeof(SensitiveAttribute)) == true;
        var notSensitive = member?.IsDefined(typeof(NotSensitiveAttribute)) == true;
        return (sensitive, notSensitive) switch
        {
            (true, false) => Sensitivity.Sensitive,
            (false, true) => Sensitivity.NotSensitive,
            (false, false) when IsPlain(Nullable.GetUnderlyingType(valueType) ?? valueType) => Sensitivity.NotSensitive,
            _ => Sensitivity.Unclassified,
        };
    }

    private static bool IsPlain(Type type)
        => type.IsPrimitive || type.IsEnum || type == typeof(decimal) || type == typeof(Guid)
           || type == typeof(DateTimeOffset) || type == typeof(DateTime) || type == typeof(DateOnly)
           || type == typeof(TimeOnly) || type == typeof(TimeSpan);
}
