namespace FullstackTemplate.UnitTests.Databases;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using Shouldly;

public class UniqueIndexExceptionTests
{
    [Test]
    public void unique_violation_is_a_conflict_on_the_field_of_the_index()
    {
        // Act
        var exception = UniqueIndexException.From(SaveFailure(PostgresErrorCodes.UniqueViolation, "ix_users_identifier")).ShouldNotBeNull();

        // Assert
        exception.Index.ShouldBe("ix_users_identifier");
        exception.Property.ShouldBe("identifier");
        DomainProblemDetails.StatusCodeFor(exception).ShouldBe(StatusCodes.Status409Conflict);
        DomainError.IsClientError(exception).ShouldBeTrue();
    }

    [Test]
    public void unique_violation_problem_has_the_message_under_the_field_of_the_index()
    {
        // Arrange
        var context = ContextFor(UniqueIndexException.From(SaveFailure(PostgresErrorCodes.UniqueViolation, "ix_users_identifier"))!);

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
        var context = ContextFor(UniqueIndexException.From(SaveFailure(PostgresErrorCodes.UniqueViolation))!);

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Detail.ShouldBe("A record with this value already exists.");
        context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldBeEmpty();
    }

    [Test]
    public void other_database_failure_is_not_a_unique_violation()
    {
        UniqueIndexException.From(SaveFailure(PostgresErrorCodes.ForeignKeyViolation)).ShouldBeNull();
    }

    [Test]
    public void each_unique_index_in_the_model_has_a_field()
    {
        // Arrange
        using var dbContext = new AppDbContextFactory().CreateDbContext([]);
        var model = dbContext.GetService<IDesignTimeModel>().Model;

        // Act
        var uniqueIndexes = model.GetEntityTypes()
            .SelectMany(e => e.GetIndexes())
            .Where(i => i.IsUnique)
            .Select(i => i.GetDatabaseName())
            .ToList();

        // Assert
        uniqueIndexes.ShouldNotBeEmpty();
        uniqueIndexes.Where(i => !UniqueIndexException.IndexFields.ContainsKey(i!)).ShouldBeEmpty();
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
