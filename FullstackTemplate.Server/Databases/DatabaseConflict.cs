namespace FullstackTemplate.Server.Databases;

using Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// Turns a failed save that is a client error into a <see cref="StaleVersionException"/> or a
/// <see cref="ConstraintConflictException"/>. It is the only code that reads the failure shapes of EF Core and Npgsql.
/// </summary>
public static class DatabaseConflict
{
    /// <summary>Null when the failure is a server fault.</summary>
    public static Exception? From(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
            return new StaleVersionException(exception);

        if (exception.InnerException is not PostgresException postgres)
            return null;

        var message = postgres.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => ConstraintConflictException.DuplicateMessage,
            PostgresErrorCodes.ExclusionViolation => ConstraintConflictException.OverlapMessage,
            _ => null,
        };
        if (message is null)
            return null;

        var constraint = postgres.ConstraintName ?? string.Empty;
        return new ConstraintConflictException(
            constraint,
            ConstraintConflictException.ConstraintFields.GetValueOrDefault(constraint, string.Empty),
            message,
            exception);
    }
}
