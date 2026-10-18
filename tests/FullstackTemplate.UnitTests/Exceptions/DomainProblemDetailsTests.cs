namespace FullstackTemplate.UnitTests.Exceptions;

using FullstackTemplate.Server.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shouldly;

public class DomainProblemDetailsTests
{
    [Fact]
    public void unique_violation_is_a_validation_problem()
    {
        // Arrange
        var exception = SaveFailure(PostgresErrorCodes.UniqueViolation);

        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);

        // Assert
        status.ShouldBe(StatusCodes.Status422UnprocessableEntity);
        DomainProblemDetails.IsClientError(exception).ShouldBeTrue();
    }

    [Fact]
    public void unique_violation_problem_does_not_show_the_constraint()
    {
        // Arrange
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = SaveFailure(PostgresErrorCodes.UniqueViolation),
            ProblemDetails = new() { Status = StatusCodes.Status422UnprocessableEntity },
        };

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Detail.ShouldBe("A record with this value already exists.");
        context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldBeEmpty();
    }

    [Fact]
    public void other_database_failure_is_a_server_error()
    {
        // Arrange
        var exception = SaveFailure(PostgresErrorCodes.ForeignKeyViolation);

        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);

        // Assert
        status.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    private static DbUpdateException SaveFailure(string sqlState)
        => new("An error occurred while saving the entity changes.",
            new PostgresException("duplicate key value violates unique constraint \"ix_users_identifier\"",
                "ERROR", "ERROR", sqlState));
}
