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

    /// <summary>
    /// The <c>sub</c> of the token of the save. It shows only to a caller with <c>read_audit_log_sensitive</c>,
    /// because a provider can make it an email address. Null for system work.
    /// </summary>
    public string? ActorSubject { get; init; }

    /// <summary>The client ID of the token of the save. Null for system work.</summary>
    public string? ActorClient { get; init; }

    /// <summary>
    /// The ID of the user who acted, also for a delegated agent. Null for system work, for a client that acts for no
    /// user, and for a user with no user record yet.
    /// </summary>
    public string? ActorUserId { get; init; }

    /// <summary>The ID of the agent that acted. Null when no agent acted.</summary>
    public string? ActorAgentId { get; init; }

    /// <summary>The MediatR request of the save, for example "UpdateUser.Command". Null for work outside a request.</summary>
    public string? RequestName { get; init; }

    public string? TraceId { get; init; }

    public IReadOnlyList<AuditValueDto> Values { get; init; } = [];
}
