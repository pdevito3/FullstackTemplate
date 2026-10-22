namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

public class DatabaseConflictTests
{
    [Test]
    public void unique_violation_is_a_conflict_on_the_field_of_the_index()
    {
        // Act
        var exception = DatabaseConflict.From(SaveFailure(PostgresErrorCodes.UniqueViolation, "ix_users_identifier"))
            .ShouldBeOfType<ConstraintConflictException>();

        // Assert
        exception.Constraint.ShouldBe("ix_users_identifier");
        exception.Property.ShouldBe("identifier");
        exception.Message.ShouldBe(ConstraintConflictException.DuplicateMessage);
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
        DomainError.IsClientError(exception).ShouldBeTrue();
    }

    [Test]
    public void unique_violation_problem_has_the_message_under_the_field_of_the_index()
    {
        // Arrange
        var context = ContextFor(DatabaseConflict.From(SaveFailure(PostgresErrorCodes.UniqueViolation, "ix_users_identifier"))!);

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        var error = context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldHaveSingleItem();
        error.Key.ShouldBe("identifier");
        error.Value.ShouldBe(["A record with this value already exists."]);
    }

    [Test]
    public void unique_violation_problem_does_not_show_the_constraint()
    {
        // Arrange
        var context = ContextFor(DatabaseConflict.From(SaveFailure(PostgresErrorCodes.UniqueViolation))!);

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Detail.ShouldBe("A record with this value already exists.");
        context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldBeEmpty();
    }

    [Test]
    public void exclusion_violation_is_an_overlap_conflict_on_the_field_of_the_constraint()
    {
        // Act
        var exception = DatabaseConflict.From(SaveFailure(PostgresErrorCodes.ExclusionViolation, "ix_tenants_name"))
            .ShouldBeOfType<ConstraintConflictException>();

        // Assert
        exception.Constraint.ShouldBe("ix_tenants_name");
        exception.Property.ShouldBe("name");
        exception.Message.ShouldBe("This value overlaps a record that exists.");
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
    }

    [Test]
    public void exclusion_violation_problem_of_an_unknown_constraint_does_not_show_the_constraint()
    {
        // Arrange
        var context = ContextFor(DatabaseConflict.From(SaveFailure(PostgresErrorCodes.ExclusionViolation, "ex_bookings_room_period"))!);

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Detail.ShouldBe("This value overlaps a record that exists.");
        context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldBeEmpty();
    }

    [Test]
    public void concurrency_failure_is_a_stale_version_conflict()
    {
        // Act
        var exception = DatabaseConflict.From(new DbUpdateConcurrencyException("The row changed."))
            .ShouldBeOfType<ConcurrencyConflictException>();

        // Assert
        exception.Property.ShouldBe("version");
        exception.Message.ShouldBe("This record changed after you loaded it. Reload it, then make your change again.");
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
    }

    [Test]
    public void other_database_failure_is_not_a_conflict()
    {
        DatabaseConflict.From(SaveFailure(PostgresErrorCodes.ForeignKeyViolation)).ShouldBeNull();
    }

    private static ProblemDetailsContext ContextFor(Exception exception) => new()
    {
        HttpContext = new DefaultHttpContext(),
        Exception = exception,
        ProblemDetails = new() { Status = StatusCodes.Status409Conflict },
    };

    private static DbUpdateException SaveFailure(string sqlState, string? constraintName = null)
        => new("An error occurred while saving the entity changes.",
            new PostgresException("duplicate key value violates unique constraint \"ix_users_identifier\"",
                "ERROR", "ERROR", sqlState, constraintName: constraintName));
}
