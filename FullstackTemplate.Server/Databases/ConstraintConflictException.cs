namespace FullstackTemplate.Server.Databases;

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

    public string Constraint { get; } = constraint;
}
