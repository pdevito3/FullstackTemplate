namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Shouldly;

public class DatabaseConflictTests
{
    // ASP.NET Core sets the default type of the status before Customize runs
    private const string DefaultConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";

    private static readonly IModel Model = new AppDbContextFactory().CreateDbContext([]).Model;

    [Test]
    public void unique_violation_is_a_conflict_with_the_field_and_the_message_of_the_index()
    {
        // Act
        var exception = DatabaseConflict.From(SaveFailure(PostgresErrorCodes.UniqueViolation, "ix_users_identifier"), Model)
            .ShouldBeOfType<ConstraintConflictException>();

        // Assert
        exception.Constraint.ShouldBe("ix_users_identifier");
        exception.Property.ShouldBe("identifier");
        exception.Message.ShouldBe("A user with this identifier already exists.");
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
        DomainError.IsClientError(exception).ShouldBeTrue();
    }

    [Test]
    public void unique_violation_problem_has_the_message_under_the_field_of_the_index()
    {
        // Arrange
        var context = ContextFor(DatabaseConflict.From(SaveFailure(PostgresErrorCodes.UniqueViolation, "ix_users_identifier"), Model)!);

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Type.ShouldBe(DefaultConflictType);
        context.ProblemDetails.Title.ShouldBe("The request conflicts with a record that exists.");
        var error = context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldHaveSingleItem();
        error.Key.ShouldBe("identifier");
        error.Value.ShouldBe(["A user with this identifier already exists."]);
    }

    [Test]
    public void unique_violation_problem_does_not_show_the_constraint()
    {
        // Arrange
        var context = ContextFor(DatabaseConflict.From(SaveFailure(PostgresErrorCodes.UniqueViolation), Model)!);

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Detail.ShouldBe("A record with this value already exists.");
        context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldBeEmpty();
    }

    [Test]
    public void exclusion_violation_is_a_conflict_with_the_field_and_the_message_of_the_constraint()
    {
        // Act
        var exception = DatabaseConflict.From(SaveFailure(PostgresErrorCodes.ExclusionViolation, "ix_tenants_name"), Model)
            .ShouldBeOfType<ConstraintConflictException>();

        // Assert
        exception.Constraint.ShouldBe("ix_tenants_name");
        exception.Property.ShouldBe("name");
        exception.Message.ShouldBe("A tenant with this name already exists.");
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
    }

    [Test]
    public void exclusion_violation_problem_of_an_unknown_constraint_does_not_show_the_constraint()
    {
        // Arrange
        var context = ContextFor(DatabaseConflict.From(SaveFailure(PostgresErrorCodes.ExclusionViolation, "ex_bookings_room_period"), Model)!);

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Detail.ShouldBe("This value overlaps a record that exists.");
        context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldBeEmpty();
    }

    [Test]
    public void concurrency_failure_is_a_stale_version()
    {
        // Act
        var exception = DatabaseConflict.From(new DbUpdateConcurrencyException("The row changed."), Model)
            .ShouldBeOfType<StaleVersionException>();

        // Assert
        exception.Message.ShouldBe("This record changed after you loaded it. Reload it, then make your change again.");
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
        DomainError.IsClientError(exception).ShouldBeTrue();
    }

    [Test]
    public void concurrency_failure_problem_has_the_stale_version_type_and_the_message_under_the_version()
    {
        // Arrange
        var context = ContextFor(DatabaseConflict.From(new DbUpdateConcurrencyException("The row changed."), Model)!);

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Type.ShouldBe(DomainProblemDetails.StaleVersionType);
        context.ProblemDetails.Title.ShouldBe("The record changed after you loaded it.");
        context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>()
            .ShouldHaveSingleItem().Key.ShouldBe("version");
    }

    [Test]
    public void other_database_failure_is_not_a_conflict()
    {
        DatabaseConflict.From(SaveFailure(PostgresErrorCodes.ForeignKeyViolation), Model).ShouldBeNull();
    }

    private static ProblemDetailsContext ContextFor(Exception exception) => new()
    {
        HttpContext = new DefaultHttpContext(),
        Exception = exception,
        ProblemDetails = new() { Status = StatusCodes.Status409Conflict, Type = DefaultConflictType },
    };

    private static DbUpdateException SaveFailure(string sqlState, string? constraintName = null)
        => new("An error occurred while saving the entity changes.",
            new PostgresException("duplicate key value violates unique constraint \"ix_users_identifier\"",
                "ERROR", "ERROR", sqlState, constraintName: constraintName));
}
