namespace FullstackTemplate.Server.Domain.AuditEntries.Dtos;

public sealed record AuditEntryDto
{
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>The save that wrote the entry. The entries of one save have the same record id.</summary>
    public Guid RecordId { get; init; }

    /// <summary>The position of the entry in its save.</summary>
    public int Ordinal { get; init; }

    /// <summary>"Created", "Updated", "Deleted", "Restored", or "Action".</summary>
    public string Kind { get; init; } = default!;

    /// <summary>The name of the action, for example "users_exported". Null for a data change.</summary>
    public string? Action { get; init; }

    public string TenantId { get; init; } = default!;
    public string EntityType { get; init; } = default!;
    public string EntityId { get; init; } = default!;
    public string RootType { get; init; } = default!;
    public string RootId { get; init; } = default!;

    /// <summary>The subject of the user of the save. Null for system work and for a machine client.</summary>
    public string? ActorUser { get; init; }

    /// <summary>The client ID of the token of the save. Null for system work.</summary>
    public string? ActorClient { get; init; }

    /// <summary>The MediatR request of the save, for example "UpdateUser.Command". Null for work outside a request.</summary>
    public string? RequestName { get; init; }

    public string? TraceId { get; init; }

    public IReadOnlyList<AuditValueDto> Values { get; init; } = [];
}
