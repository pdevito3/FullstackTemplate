namespace FullstackTemplate.Server.Databases;

using Exceptions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A save that a unique index or an exclusion constraint stopped. <see cref="AppDbContext"/> throws it in place of the
/// <see cref="DbUpdateException"/>. The error names the request field of the constraint. The constraint name and the
/// values stay out of the error, because they show the schema and other rows.
/// </summary>
public sealed class ConstraintConflictException(
    string constraint,
    string property,
    string message,
    DbUpdateException innerException)
    : ConflictException(property, message, innerException)
{
    public const string DuplicateMessage = "A record with this value already exists.";
    public const string OverlapMessage = "This value overlaps a record that exists.";

    public static readonly IReadOnlyDictionary<string, string> ConstraintFields = new Dictionary<string, string>
    {
        ["ix_tenants_name"] = "name",
        ["ix_users_identifier"] = "identifier",
        ["ix_agents_tenant_id_client_id"] = "clientId",
        ["ix_agents_client_id"] = "clientId",
    };

    public string Constraint { get; } = constraint;
}
