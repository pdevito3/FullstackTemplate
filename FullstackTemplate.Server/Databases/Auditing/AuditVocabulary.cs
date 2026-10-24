namespace FullstackTemplate.Server.Databases.Auditing;

using System.Reflection;
using System.Text.Json.Serialization;
using Domain.AuditEntries;
using Microsoft.EntityFrameworkCore.Metadata;

/// <summary>
/// The values that the code of today knows, with the sensitivity of each: the recorded values of each entity type by
/// class name, and the properties of each action by action name. The read hides each value that it does not know,
/// for example the value of a removed column or of a removed action (fail closed).
/// </summary>
internal sealed class AuditVocabulary(
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, Sensitivity>> entityTypes,
    IReadOnlyDictionary<string, IReadOnlyDictionary<string, Sensitivity>> actions)
{
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, Sensitivity>> EntityTypes { get; } = entityTypes;
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, Sensitivity>> Actions { get; } = actions;

    public Sensitivity SensitivityOf(AuditEntryKind kind, string entityType, string? action, string path)
    {
        var known = kind == AuditEntryKind.Action
            ? action is null ? null : Actions.GetValueOrDefault(action)
            : EntityTypes.GetValueOrDefault(entityType);
        return known?.GetValueOrDefault(path, Sensitivity.Unclassified) ?? Sensitivity.Unclassified;
    }

    /// <exception cref="InvalidOperationException">An action type has no name, or two action types share a name.</exception>
    public static AuditVocabulary Of(
        IReadOnlyDictionary<IEntityType, IReadOnlyList<AuditedValue>> values, IEnumerable<Type> types)
    {
        var entityTypes = values.ToDictionary(
            pair => pair.Key.ClrType.Name,
            IReadOnlyDictionary<string, Sensitivity> (pair) => pair.Value
                .Where(value => value.IsRecorded)
                .ToDictionary(value => value.Path, value => value.Sensitivity));

        var actions = new Dictionary<string, IReadOnlyDictionary<string, Sensitivity>>();
        var actionTypes = new Dictionary<string, Type>();
        foreach (var type in types.Where(t => t is { IsAbstract: false } && t.IsSubclassOf(typeof(AuditAction))))
        {
            var name = AuditAction.NameOf(type);
            if (!actionTypes.TryAdd(name, type))
                throw new InvalidOperationException(
                    $"{actionTypes[name].Name} and {type.Name} have the audit action name '{name}'. Give each action a name of its own.");

            actions[name] = StoredProperties(type)
                .ToDictionary(property => property.Name, property => AuditSensitivity.Of(property, property.PropertyType, isForeignKey: false));
        }

        return new AuditVocabulary(entityTypes, actions);
    }

    /// <summary>The properties that <see cref="AuditValues.WriteAction"/> stores: each public one except the subject.</summary>
    private static IEnumerable<PropertyInfo> StoredProperties(Type actionType)
        => actionType.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => !property.IsDefined(typeof(JsonIgnoreAttribute)));
}
