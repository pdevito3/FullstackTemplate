namespace FullstackTemplate.Server.Databases;

using Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;

/// <summary>
/// Turns a failed save or bulk insert that is a client error into a <see cref="StaleVersionException"/> or a
/// <see cref="ConstraintConflictException"/>.
/// </summary>
public static class DatabaseConflict
{
    /// <summary>Null when the failure is a server fault. The model gives the <see cref="ConstraintErrors"/>.</summary>
    public static Exception? From(DbUpdateException exception, IModel model)
    {
        if (exception is DbUpdateConcurrencyException)
            return new StaleVersionException(exception);

        return exception.InnerException is PostgresException postgres ? From(postgres, exception, model) : null;
    }

    /// <summary>For a write that EF does not run, such as COPY. Null when the failure is a server fault.</summary>
    public static Exception? From(PostgresException exception, IModel model) => From(exception, exception, model);

    private static Exception? From(PostgresException postgres, Exception exception, IModel model)
    {
        var defaultMessage = postgres.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => ConstraintConflictException.DuplicateMessage,
            PostgresErrorCodes.ExclusionViolation => ConstraintConflictException.OverlapMessage,
            _ => null,
        };
        if (defaultMessage is null)
            return null;

        var constraint = postgres.ConstraintName ?? string.Empty;
        return ConstraintErrors.Of(model).GetValueOrDefault(constraint) switch
        {
            ConstraintError.StaleVersion => new StaleVersionException(exception),
            ConstraintError.RequestField error => new ConstraintConflictException(constraint, error.Field, error.Message, exception),
            _ => new ConstraintConflictException(constraint, string.Empty, defaultMessage, exception),
        };
    }
}
