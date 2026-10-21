namespace FullstackTemplate.UnitTests.Resources;

using System.ComponentModel.DataAnnotations;
using System.Reflection;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Users.Dtos;
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Domain.Weather.Features;
using FullstackTemplate.Server.Exceptions;
using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Resources.Entrypoints.Http;
using MediatR;
using Shouldly;

public class RequestValidationBehaviorTests
{
    [Test]
    public async Task a_page_out_of_range_gives_an_error_for_each_field()
    {
        // Arrange
        var parameters = new TenantParametersDto { PageNumber = 0, PageSize = -1 };

        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(
            () => Send(new GetTenantList.Query(parameters)));

        // Assert
        exception.Errors.Keys.ShouldBe(["pageNumber", "pageSize"], ignoreOrder: true);
        exception.Errors["pageNumber"].ShouldBe(["The field pageNumber must be between 1 and 4294967."]);
        exception.Errors["pageSize"].ShouldBe(["The field pageSize must be between 1 and 2147483647."]);
    }

    [Test]
    public async Task a_missing_field_of_the_dto_gives_the_json_name()
    {
        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(
            () => Send(new AddUser.Command(new UserForCreationDto { FirstName = "Ada" })));

        // Assert
        exception.Errors.Keys.ShouldBe(["lastName", "identifier", "email", "username", "role"], ignoreOrder: true);
        exception.Errors["lastName"].ShouldBe(["The lastName field is required."]);
    }

    [Test]
    public async Task a_missing_field_of_the_command_gives_the_json_name()
    {
        // Act
        var exception = await Should.ThrowAsync<RequestValidationException>(
            () => Send(new UpdateUserRole.Command("usr_1", null!)));

        // Assert
        exception.Errors.ShouldHaveSingleItem().Key.ShouldBe("role");
    }

    [Test]
    public async Task an_empty_string_goes_to_the_handler()
    {
        // Act
        var sent = await Send(new AddTenant.Command(new TenantForCreationDto { Name = "" }));

        // Assert
        sent.ShouldBeTrue();
    }

    [Test]
    public async Task a_large_page_size_is_limited_and_goes_to_the_handler()
    {
        // Act
        var sent = await Send(new GetTenantList.Query(new TenantParametersDto { PageSize = 5000 }));

        // Assert
        sent.ShouldBeTrue();
    }

    /// <summary>
    /// The HTTP endpoints keep <c>[Range]</c> on their parameters only for the OpenAPI contract.
    /// The behavior applies the rules of the request or its DTO, so each copy must be the same as that rule.
    /// </summary>
    [Test]
    public void each_range_of_an_endpoint_parameter_is_the_range_of_the_dto()
    {
        // Arrange
        var dtoRanges = new[] { typeof(BasePaginationParameters), typeof(GetWeatherForecast.Query) }
            .SelectMany(type => type.GetProperties())
            .Where(property => property.GetCustomAttribute<RangeAttribute>() is not null)
            .ToDictionary(property => property.Name, property => property.GetCustomAttribute<RangeAttribute>()!, StringComparer.OrdinalIgnoreCase);
        var parameters = typeof(IEndpoint).Assembly.GetTypes()
            .Where(type => typeof(IEndpoint).IsAssignableFrom(type) && !type.IsInterface)
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .SelectMany(method => method.GetParameters())
            .Where(parameter => parameter.GetCustomAttribute<RangeAttribute>() is not null)
            .ToList();

        // Assert
        parameters.ShouldNotBeEmpty();
        foreach (var parameter in parameters)
        {
            var range = parameter.GetCustomAttribute<RangeAttribute>()!;
            dtoRanges.ShouldContainKey(parameter.Name!);
            var dtoRange = dtoRanges[parameter.Name!];
            (range.Minimum, range.Maximum).ShouldBe((dtoRange.Minimum, dtoRange.Maximum), $"{parameter.Member.DeclaringType!.Name}.{parameter.Name}");
        }
    }

    /// <summary>
    /// The behavior does not read nullable annotations. Without <see cref="RequiredFieldAttribute"/>, a missing string
    /// binds to null and goes to the handler, so each request DTO must mark each string that is not nullable.
    /// </summary>
    [Test]
    public void each_string_of_a_request_dto_that_is_not_nullable_is_a_required_field()
    {
        // Arrange
        var nullability = new NullabilityInfoContext();
        var requestDtos = typeof(RequestValidationBehavior<,>).Assembly.GetTypes()
            .Where(type => typeof(IBaseRequest).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false })
            .SelectMany(request => request.GetProperties())
            .Select(property => property.PropertyType)
            .Where(type => type.IsClass && type != typeof(string) && type.Assembly == typeof(RequestValidationBehavior<,>).Assembly)
            .Distinct()
            .ToList();

        // Act
        var unmarked = requestDtos
            .SelectMany(dto => dto.GetProperties().Select(property => (Dto: dto, Property: property)))
            .Where(item => item.Property.PropertyType == typeof(string)
                && item.Property.SetMethod is not null
                && nullability.Create(item.Property).WriteState == NullabilityState.NotNull
                && item.Property.GetCustomAttribute<RequiredFieldAttribute>() is null)
            .Select(item => $"{item.Dto.Name}.{item.Property.Name}")
            .ToList();

        // Assert
        requestDtos.ShouldContain(typeof(UserForCreationDto));
        unmarked.ShouldBeEmpty();
    }

    private static async Task<bool> Send<TResponse>(IRequest<TResponse> request)
    {
        var sent = false;
        await new RequestValidationBehavior<IRequest<TResponse>, TResponse>().Handle(request, _ =>
        {
            sent = true;
            return Task.FromResult(default(TResponse)!);
        }, CancellationToken.None);
        return sent;
    }
}
