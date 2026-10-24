namespace FullstackTemplate.Server.Domain.AuditEntries;

using Databases.Auditing;
using Dtos;

internal static class AuditRedaction
{
    /// <summary>
    /// The entry as the caller may read it. A value shows when the caller can read sensitive values, or when the
    /// vocabulary knows it as not sensitive. A value of an entity type, an action, or a path that the code no longer
    /// knows is hidden (fail closed). The names always show, because they are code, not data.
    /// </summary>
    public static AuditEntryDto Redact(AuditEntry entry, AuditVocabulary vocabulary, bool canReadSensitive) => new()
    {
        OccurredAt = entry.OccurredAt,
        RecordId = entry.RecordId,
        Ordinal = entry.Ordinal,
        Kind = entry.Kind.ToString(),
        Action = entry.Action,
        TenantId = entry.TenantId,
        EntityType = entry.EntityType,
        EntityId = entry.EntityId,
        RootType = entry.RootType,
        RootId = entry.RootId,
        ActorUser = entry.ActorUser,
        ActorClient = entry.ActorClient,
        RequestName = entry.RequestName,
        TraceId = entry.TraceId,
        Values = AuditValues.Read(entry.Kind, entry.Data)
            .Select(value => canReadSensitive
                             || vocabulary.SensitivityOf(entry.Kind, entry.EntityType, entry.Action, value.Name)
                             == Sensitivity.NotSensitive
                ? new AuditValueDto { Name = value.Name, Old = value.Old, New = value.New }
                : new AuditValueDto { Name = value.Name, Hidden = true })
            .ToList(),
    };
}
