namespace FullstackTemplate.Server.Databases;

using Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// A save that a unique index stopped. A handler checks for a duplicate first, but two requests can pass the check at
/// the same time. Then the index stops the second save. That is a client conflict, not a server fault, so
/// <see cref="AppDbContext"/> throws this exception in place of the <see cref="DbUpdateException"/>.
/// The error names the request field of the index. The index name and the values stay out of the error, because
/// they show the schema and other rows.
/// </summary>
public sealed class UniqueIndexException(string index, string property, DbUpdateException innerException)
    : ConflictException(property, DuplicateMessage, innerException)
{
    public const string DuplicateMessage = "A record with this value already exists.";

    /// <summary>
    /// The request field of each unique index. A unit test makes sure that each unique index in the model has an entry.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> IndexFields = new Dictionary<string, string>
    {
        ["ix_tenants_name"] = "name",
        ["ix_users_identifier"] = "identifier",
    };

    /// <summary>The name of the index that stopped the save.</summary>
    public string Index { get; } = index;

    /// <summary>Null when a unique index did not cause the failure.</summary>
    public static UniqueIndexException? From(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgres)
            return null;
        var index = postgres.ConstraintName ?? string.Empty;
        return new(index, IndexFields.GetValueOrDefault(index, string.Empty), exception);
    }
}
