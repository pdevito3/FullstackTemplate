namespace FullstackTemplate.Server.Domain.AuditEntries.Dtos;

public sealed record AuditValueDto
{
    /// <summary>The property, for example "FirstName", or "Address.City" for one value of a complex property.</summary>
    public string Name { get; init; } = default!;

    /// <summary>The value before the change. Null for a created value, for an action, and for a hidden value.</summary>
    public string? Old { get; init; }

    /// <summary>The value after the change, or the value of an action. Null for a hidden value.</summary>
    public string? New { get; init; }

    /// <summary>
    /// True when the caller has no <c>read_audit_log_sensitive</c> and the value is sensitive, or is a value that the
    /// code no longer knows.
    /// </summary>
    public bool Hidden { get; init; }
}
