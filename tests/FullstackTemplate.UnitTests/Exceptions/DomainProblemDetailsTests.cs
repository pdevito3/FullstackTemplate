namespace FullstackTemplate.UnitTests.Exceptions;

using FullstackTemplate.Server.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QueryKit.Exceptions;
using Shouldly;

public class DomainProblemDetailsTests
{
    [Fact]
    public void unique_violation_is_a_conflict()
    {
        // Arrange
        var exception = SaveFailure(PostgresErrorCodes.UniqueViolation);

        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);

        // Assert
        status.ShouldBe(StatusCodes.Status409Conflict);
        DomainProblemDetails.IsClientError(exception).ShouldBeTrue();
    }

    [Fact]
    public void conflict_problem_has_the_message_under_the_property()
    {
        // Arrange
        var exception = new ConflictException("identifier", "A user with this identifier already exists.");
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = exception,
            ProblemDetails = new() { Status = StatusCodes.Status409Conflict },
        };

        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);
        DomainProblemDetails.Customize(context);

        // Assert
        status.ShouldBe(StatusCodes.Status409Conflict);
        context.ProblemDetails.Detail.ShouldBe("A user with this identifier already exists.");
        var error = context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldHaveSingleItem();
        error.Key.ShouldBe("identifier");
        error.Value.ShouldBe(["A user with this identifier already exists."]);
    }

    [Fact]
    public void unique_violation_problem_does_not_show_the_constraint()
    {
        // Arrange
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = SaveFailure(PostgresErrorCodes.UniqueViolation),
            ProblemDetails = new() { Status = StatusCodes.Status409Conflict },
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

    public static readonly TheoryData<Exception, int> ExpectedStatusCodes = new()
    {
        { new ValidationException("name", "Please provide a name."), StatusCodes.Status422UnprocessableEntity },
        { new FluentValidation.ValidationException("Please provide a valid email."), StatusCodes.Status422UnprocessableEntity },
        { new QueryInputException("sortOrder", "Parsing failed during sorting.", new SortParsingException("Unknown")), StatusCodes.Status400BadRequest },
        { new NotFoundException("Tenant was not found."), StatusCodes.Status404NotFound },
        { new ForbiddenAccessException("Only an admin can do this."), StatusCodes.Status403Forbidden },
        { new ConflictException("identifier", "A user with this identifier already exists."), StatusCodes.Status409Conflict },
        // These two come from the setup of the server, not from the request
        { new QueryKitDbContextTypeException("SoundsLike"), StatusCodes.Status500InternalServerError },
        { new SoundsLikeNotImplementedException("AppDbContext"), StatusCodes.Status500InternalServerError },
        { new InvalidOperationException("x"), StatusCodes.Status500InternalServerError },
    };

    [Theory]
    [MemberData(nameof(ExpectedStatusCodes))]
    public void each_exception_maps_to_its_status(Exception exception, int expected)
    {
        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);

        // Assert
        status.ShouldBe(expected);
    }

    private static DbUpdateException SaveFailure(string sqlState)
        => new("An error occurred while saving the entity changes.",
            new PostgresException("duplicate key value violates unique constraint \"ix_users_identifier\"",
                "ERROR", "ERROR", sqlState));
}
