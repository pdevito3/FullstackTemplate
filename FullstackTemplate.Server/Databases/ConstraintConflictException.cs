namespace FullstackTemplate.Server.Databases;

using Exceptions;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// A save that a unique index or an exclusion constraint stopped. A handler checks for a duplicate first, but two
/// requests can pass the check at the same time. Then the constraint stops the second save. That is a client
/// conflict, not a server fault, so <see cref="AppDbContext"/> throws this exception in place of the
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

    /// <summary>
    /// The request field of each unique index and exclusion constraint. A test makes sure that each of them in the
    /// schema has an entry.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> ConstraintFields = new Dictionary<string, string>
    {
        ["ix_tenants_name"] = "name",
        ["ix_users_identifier"] = "identifier",
    };

    /// <summary>The name of the constraint that stopped the save.</summary>
    public string Constraint { get; } = constraint;
}
