namespace FullstackTemplate.Server.Databases;

using Domain.Agents;
using Domain.Tenants;
using Domain.Users;
using Exceptions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A save or a bulk insert that a unique index or an exclusion constraint stopped. <see cref="AppDbContext"/> throws it
/// in place of the <see cref="DbUpdateException"/> or, for COPY, the <see cref="Npgsql.PostgresException"/>. The error
/// has the request field and the message of the constraint, so the loser of a race past the check of a handler gets the
/// message of the check. The constraint name and the values stay out of the error, because they show the schema and
/// other rows.
/// </summary>
public sealed class ConstraintConflictException(
    string constraint,
    string property,
    string message,
    Exception innerException)
    : ConflictException(property, message, innerException)
{
    public const string DuplicateMessage = "A record with this value already exists.";
    public const string OverlapMessage = "This value overlaps a record that exists.";

    public static readonly IReadOnlyDictionary<string, ConstraintError> ConstraintErrors = new Dictionary<string, ConstraintError>
    {
        ["ix_tenants_name"] = new("name", Tenant.NameInUseMessage),
        ["ix_users_identifier"] = new("identifier", User.IdentifierInUseMessage),
        ["ix_agents_tenant_id_client_id"] = new("clientId", Agent.ClientIdInUseMessage),
        ["ix_agents_client_id"] = new("clientId", Agent.ClientIdInUseMessage),
    };

    public string Constraint { get; } = constraint;
}
