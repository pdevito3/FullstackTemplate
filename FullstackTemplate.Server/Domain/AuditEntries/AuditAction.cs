namespace FullstackTemplate.Server.Domain.AuditEntries;

using System.Reflection;
using System.Text.Json.Serialization;

/// <summary>
/// A fact that a handler records about an aggregate root and that is not a data change, for example "users exported".
/// Declare each action as a sealed record that derives from this type and has an <see cref="AuditActionNameAttribute"/>.
/// Record it with <c>dbContext.RecordAction(action)</c>, then save. The save writes it in the same transaction and the
/// same record as its data changes. The entry holds each property of the action except <see cref="Subject"/>.
/// </summary>
public abstract record AuditAction([property: JsonIgnore] AuditSubject Subject)
{
    /// <exception cref="InvalidOperationException">The type has no <see cref="AuditActionNameAttribute"/>.</exception>
    internal static string NameOf(Type actionType)
        => actionType.GetCustomAttribute<AuditActionNameAttribute>(inherit: false)?.Name
           ?? throw new InvalidOperationException(
               $"{actionType.Name} has no [AuditActionName]. Give each audit action the name that its entries store.");
}

/// <summary>
/// The stored name of an action in snake case, for example "users_exported". Like a permission name, it does not change
/// after release, because a stored entry finds its action type by this name.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class AuditActionNameAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}
