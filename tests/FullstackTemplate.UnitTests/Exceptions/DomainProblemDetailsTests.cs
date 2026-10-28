namespace FullstackTemplate.UnitTests.Exceptions;

using FullstackTemplate.Server.Databases;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources.Entrypoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using QueryKit.Exceptions;
using Shouldly;

public class DomainProblemDetailsTests
{
    // ASP.NET Core sets the default type of the status before Customize runs
    private const string DefaultConflictType = "https://tools.ietf.org/html/rfc9110#section-15.5.10";
    private const string StaleMessage = "This record changed after you loaded it. Reload it, then make your change again.";

    [Test]
    public void conflict_problem_has_the_message_under_the_property()
    {
        // Arrange
        var exception = new ConflictException("identifier", "A user with this identifier already exists.");
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = exception,
            ProblemDetails = new() { Status = StatusCodes.Status409Conflict, Type = DefaultConflictType },
        };

        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);
        DomainProblemDetails.Customize(context);

        // Assert
        status.ShouldBe(StatusCodes.Status409Conflict);
        context.ProblemDetails.Type.ShouldBe(DefaultConflictType);
        context.ProblemDetails.Title.ShouldBe("The request conflicts with a record that exists.");
        context.ProblemDetails.Detail.ShouldBe("A user with this identifier already exists.");
        var error = context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldHaveSingleItem();
        error.Key.ShouldBe("identifier");
        error.Value.ShouldBe(["A user with this identifier already exists."]);
    }

    [Test]
    public void request_validation_problem_has_the_messages_under_each_field()
    {
        // Arrange
        var exception = new RequestValidationException(new()
        {
            ["pageNumber"] = ["The field pageNumber must be between 1 and 4294967."],
            ["pageSize"] = ["The field pageSize must be between 1 and 2147483647."],
        });
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = exception,
            ProblemDetails = new() { Status = StatusCodes.Status400BadRequest },
        };

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Title.ShouldBe("One or more validation errors occurred.");
        var errors = context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>();
        errors.Keys.ShouldBe(["pageNumber", "pageSize"]);
        errors["pageSize"].ShouldBe(["The field pageSize must be between 1 and 2147483647."]);
    }

    [Test]
    public void validation_problem_with_no_property_has_empty_errors()
    {
        // Arrange
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = new ValidationException("Please provide a name."),
            ProblemDetails = new() { Status = StatusCodes.Status422UnprocessableEntity },
        };

        // Act
        DomainProblemDetails.Customize(context);

        // Assert
        context.ProblemDetails.Detail.ShouldBe("Please provide a name.");
        // A 422 has the ValidationProblemDetails shape, so errors is present when no field is known
        context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldBeEmpty();
    }

    [Test]
    public void stale_version_problem_has_its_own_type_and_the_message_under_the_version()
    {
        // Arrange
        var exception = new StaleVersionException();
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = exception,
            ProblemDetails = new() { Status = StatusCodes.Status409Conflict, Type = DefaultConflictType },
        };

        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);
        DomainProblemDetails.Customize(context);

        // Assert
        status.ShouldBe(StatusCodes.Status409Conflict);
        context.ProblemDetails.Type.ShouldBe("urn:fullstacktemplate:problem:stale-version");
        context.ProblemDetails.Title.ShouldBe("The record changed after you loaded it.");
        context.ProblemDetails.Detail.ShouldBe(StaleMessage);
        var error = context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldHaveSingleItem();
        error.Key.ShouldBe("version");
        error.Value.ShouldBe([StaleMessage]);
    }

    [Test]
    public void concurrent_change_problem_has_its_own_type_and_no_field()
    {
        // Arrange
        var exception = new ConcurrentChangeException();
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = exception,
            ProblemDetails = new() { Status = StatusCodes.Status409Conflict, Type = DefaultConflictType },
        };

        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);
        DomainProblemDetails.Customize(context);

        // Assert
        status.ShouldBe(StatusCodes.Status409Conflict);
        context.ProblemDetails.Type.ShouldBe("urn:fullstacktemplate:problem:concurrent-change");
        context.ProblemDetails.Title.ShouldBe("Other users changed the record at the same time.");
        context.ProblemDetails.Detail.ShouldBe(
            "Other users changed this record at the same time, so the change did not save. Send it again.");
        context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldBeEmpty();
    }

    [Test]
    public void exclusion_violation_problem_is_a_conflict_with_the_message_under_the_field()
    {
        // Arrange
        var exception = DatabaseConflict.From(SaveFailure(PostgresErrorCodes.ExclusionViolation, "ix_tenants_name"))!;
        var context = new ProblemDetailsContext
        {
            HttpContext = new DefaultHttpContext(),
            Exception = exception,
            ProblemDetails = new() { Status = StatusCodes.Status409Conflict, Type = DefaultConflictType },
        };

        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);
        DomainProblemDetails.Customize(context);

        // Assert
        status.ShouldBe(StatusCodes.Status409Conflict);
        context.ProblemDetails.Type.ShouldBe(DefaultConflictType);
        context.ProblemDetails.Title.ShouldBe("The request conflicts with a record that exists.");
        context.ProblemDetails.Detail.ShouldBe("This value overlaps a record that exists.");
        var error = context.ProblemDetails.Extensions["errors"].ShouldBeOfType<Dictionary<string, string[]>>().ShouldHaveSingleItem();
        error.Key.ShouldBe("name");
        error.Value.ShouldBe(["This value overlaps a record that exists."]);
    }

    [Test]
    public void database_failure_that_the_db_context_does_not_translate_is_a_server_error()
    {
        // Arrange
        var exception = SaveFailure(PostgresErrorCodes.ForeignKeyViolation);

        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);

        // Assert
        status.ShouldBe(StatusCodes.Status500InternalServerError);
    }

    public static IEnumerable<(Exception, int)> ExpectedStatusCodes =>
    [
        (new ValidationException("name", "Please provide a name."), StatusCodes.Status422UnprocessableEntity),
        (new FluentValidation.ValidationException("Please provide a valid email."), StatusCodes.Status422UnprocessableEntity),
        (new RequestValidationException(new() { ["pageNumber"] = ["The field pageNumber must be between 1 and 4294967."] }), StatusCodes.Status400BadRequest),
        (new QueryInputException("sortOrder", "Parsing failed during sorting.", new SortParsingException("Unknown")), StatusCodes.Status400BadRequest),
        (new NotFoundException("Tenant was not found."), StatusCodes.Status404NotFound),
        (new ForbiddenAccessException("Only an admin can do this."), StatusCodes.Status403Forbidden),
        (new ConflictException("identifier", "A user with this identifier already exists."), StatusCodes.Status409Conflict),
        (new StaleVersionException(), StatusCodes.Status409Conflict),
        (new ConcurrentChangeException(), StatusCodes.Status409Conflict),
        // These two come from the setup of the server, not from the request
        (new QueryKitDbContextTypeException("SoundsLike"), StatusCodes.Status500InternalServerError),
        (new SoundsLikeNotImplementedException("AppDbContext"), StatusCodes.Status500InternalServerError),
        (new InvalidOperationException("x"), StatusCodes.Status500InternalServerError),
    ];

    [Test]
    [MethodDataSource(nameof(ExpectedStatusCodes))]
    public void each_exception_maps_to_its_status(Exception exception, int expected)
    {
        // Act
        var status = DomainProblemDetails.StatusCodeFor(exception);

        // Assert
        status.ShouldBe(expected);
    }

    private static DbUpdateException SaveFailure(string sqlState, string? constraintName = null)
        => new("An error occurred while saving the entity changes.",
            new PostgresException("duplicate key value violates unique constraint \"ix_users_identifier\"",
                "ERROR", "ERROR", sqlState, constraintName: constraintName));
}
