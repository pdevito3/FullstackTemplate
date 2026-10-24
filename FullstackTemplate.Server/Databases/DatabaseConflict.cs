namespace FullstackTemplate.Server.Databases;

using Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

/// <summary>
/// Turns a failed save or bulk insert that is a client error into a <see cref="StaleVersionException"/> or a
/// <see cref="ConstraintConflictException"/>.
/// </summary>
public static class DatabaseConflict
{
    /// <summary>Null when the failure is a server fault.</summary>
    public static Exception? From(DbUpdateException exception)
    {
        if (exception is DbUpdateConcurrencyException)
            return new StaleVersionException(exception);

        return exception.InnerException is PostgresException postgres ? From(postgres, exception) : null;
    }

    /// <summary>For a write that EF does not run, such as COPY. Null when the failure is a server fault.</summary>
    public static Exception? From(PostgresException exception) => From(exception, exception);

    private static ConstraintConflictException? From(PostgresException postgres, Exception exception)
    {
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
