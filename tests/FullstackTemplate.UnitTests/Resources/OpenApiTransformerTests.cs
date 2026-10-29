namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Resources;
using FullstackTemplate.Server.Resources.Entrypoints.Http.OpenApi;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;

[NotInParallel(TestConstraints.HostEnvironment)]
public class OpenApiTransformerTests
{
    // The transformers read only the document, so the context needs no real values.
    private static readonly OpenApiDocumentTransformerContext Context = new()
    {
        DocumentName = "v1",
        DescriptionGroups = [],
        ApplicationServices = Substitute.For<IServiceProvider>(),
    };

    [Test]
    public async Task operation_id_transformer_accepts_named_operations()
    {
        // Arrange
        var document = DocumentWith(("/api/v1/tenants/{id}", HttpMethod.Get, Operation("GetTenant", paged: false)));

        // Act
        var act = () => new RequireOperationIdTransformer().TransformAsync(document, Context, CancellationToken.None);

        // Assert
        await act.ShouldNotThrowAsync();
    }

    [Test]
    [Arguments(null)]
    [Arguments("")]
    [Arguments(" ")]
    public async Task operation_id_transformer_rejects_an_operation_with_no_name(string? operationId)
    {
        // Arrange
        var document = DocumentWith(("/api/v1/tenants/{id}", HttpMethod.Get, Operation(operationId, paged: false)));

        // Act
        var act = () => new RequireOperationIdTransformer().TransformAsync(document, Context, CancellationToken.None);

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain("GET /api/v1/tenants/{id}");
    }

    [Test]
    public async Task list_name_transformer_accepts_names_that_match_the_list_shape()
    {
        // Arrange
        var document = DocumentWith(
            ("/api/v1/tenants", HttpMethod.Get, Operation("GetTenantList", paged: true)),
            ("/api/v1/tenants/{id}", HttpMethod.Get, Operation("GetTenant", paged: false)));

        // Act
        var act = () => new RequireListOperationNameTransformer().TransformAsync(document, Context, CancellationToken.None);

        // Assert
        await act.ShouldNotThrowAsync();
    }

    [Test]
    public async Task list_name_transformer_rejects_a_paged_operation_whose_name_does_not_end_in_list()
    {
        // Arrange
        var document = DocumentWith(("/api/v1/tenants", HttpMethod.Get, Operation("GetTenants", paged: true)));

        // Act
        var act = () => new RequireListOperationNameTransformer().TransformAsync(document, Context, CancellationToken.None);

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain("GET /api/v1/tenants (GetTenants)");
        exception.Message.ShouldContain("does not end in 'List'");
    }

    [Test]
    public async Task list_name_transformer_rejects_an_unpaged_operation_whose_name_ends_in_list()
    {
        // Arrange
        var document = DocumentWith(("/api/v1/tenants/{id}", HttpMethod.Get, Operation("GetTenantList", paged: false)));

        // Act
        var act = () => new RequireListOperationNameTransformer().TransformAsync(document, Context, CancellationToken.None);

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain("GET /api/v1/tenants/{id} (GetTenantList)");
        exception.Message.ShouldContain("do not send the X-Pagination header");
    }

    [Test]
    public async Task unauthorized_transformer_adds_401_with_no_content_to_a_secured_operation()
    {
        // Arrange
        var operation = Operation("GetTenant", paged: false);

        // Act
        await new UnauthorizedResponseTransformer()
            .TransformAsync(operation, OperationContext(new AuthorizeAttribute()), CancellationToken.None);

        // Assert
        var response = operation.Responses.ShouldNotBeNull()["401"].ShouldBeOfType<OpenApiResponse>();
        response.Content.ShouldBeNull();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task unauthorized_transformer_skips_an_operation_that_allows_anonymous_callers(bool hasAuthorize)
    {
        // Arrange
        var operation = Operation("GetWeatherForecast", paged: false);
        object[] metadata = hasAuthorize ? [new AuthorizeAttribute(), new AllowAnonymousAttribute()] : [];

        // Act
        await new UnauthorizedResponseTransformer()
            .TransformAsync(operation, OperationContext(metadata), CancellationToken.None);

        // Assert
        operation.Responses.ShouldNotBeNull().Keys.ShouldBe(["200"]);
    }

    [Test]
    public async Task non_nullable_transformer_keeps_a_nullable_reference_to_a_schema_optional()
    {
        // Arrange
        var builder = WebApplication.CreateSlimBuilder();
        // The Server appsettings.json in the test output sets Kestrel endpoints on 8080 and 8081, and they win over UseUrls
        builder.Configuration["Kestrel:Endpoints:http:Url"] = "http://127.0.0.1:0";
        builder.Configuration["Kestrel:Endpoints:grpc:Url"] = "http://127.0.0.1:0";
        builder.Services.AddOpenApi(options => options.AddSchemaTransformer<RequireNonNullablePropertiesTransformer>());
        await using var app = builder.Build();
        app.MapGet("/shipments", () => new ShipmentDto());
        // The app adds its endpoints to the routing data when it starts
        await app.StartAsync();

        // Act
        var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var schemas = document.Components.ShouldNotBeNull().Schemas.ShouldNotBeNull();
        schemas[nameof(ShipmentDto)].Required.ShouldNotBeNull().ShouldBe(["reference", "origin"], ignoreOrder: true);
    }

    private sealed record ShipmentDto
    {
        public string Reference { get; init; } = default!;
        public string? Note { get; init; }
        public AddressDto Origin { get; init; } = default!;
        public AddressDto? ShipTo { get; init; }
    }

    private sealed record AddressDto
    {
        public string City { get; init; } = default!;
    }

    [Test]
    public async Task limits_transformer_writes_the_max_length_of_the_named_constant()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((LimitedDto dto) => dto);

        // Act
        var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var properties = document.Components.ShouldNotBeNull().Schemas.ShouldNotBeNull()[nameof(LimitedDto)].Properties.ShouldNotBeNull();
        properties["name"].MaxLength.ShouldBe(Limits.NameMaxLength);
    }

    [Test]
    public async Task limits_transformer_writes_the_maximum_of_the_named_static_field()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((LimitedDto dto) => dto);

        // Act
        var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var properties = document.Components.ShouldNotBeNull().Schemas.ShouldNotBeNull()[nameof(LimitedDto)].Properties.ShouldNotBeNull();
        properties["quantity"].Maximum.ShouldBe("99.5");
    }

    [Test]
    public async Task limits_transformer_gives_a_uint_the_domain_maximum_in_place_of_the_uint_maximum()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((LimitedDto dto) => dto);

        // Act
        var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var properties = document.Components.ShouldNotBeNull().Schemas.ShouldNotBeNull()[nameof(LimitedDto)].Properties.ShouldNotBeNull();
        properties["version"].Minimum.ShouldBe("0");
        properties["version"].Maximum.ShouldBe("1000");
    }

    [Test]
    public async Task limits_transformer_writes_the_minimum_of_the_named_static_field()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((LimitedDto dto) => dto);

        // Act
        var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var properties = document.Components.ShouldNotBeNull().Schemas.ShouldNotBeNull()[nameof(LimitedDto)].Properties.ShouldNotBeNull();
        properties["quantity"].Minimum.ShouldBe("0.5");
        properties["quantity"].Maximum.ShouldBe("99.5");
    }

    [Test]
    public async Task limits_transformer_gives_a_uint_the_domain_minimum_in_place_of_zero()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((LimitedDto dto) => dto);

        // Act
        var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var properties = document.Components.ShouldNotBeNull().Schemas.ShouldNotBeNull()[nameof(LimitedDto)].Properties.ShouldNotBeNull();
        properties["seats"].Minimum.ShouldBe("1");
        properties["seats"].Maximum.ShouldBe("4294967295");
    }

    [Test]
    public async Task limits_transformer_rejects_a_max_length_on_a_number()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((MaxLengthOnNumberDto dto) => dto);

        // Act
        var act = () => app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain($"{nameof(MaxLengthOnNumberDto)}.quantity has [SchemaMaxLength]");
    }

    [Test]
    public async Task limits_transformer_rejects_a_maximum_on_a_string()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((MaximumOnStringDto dto) => dto);

        // Act
        var act = () => app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain($"{nameof(MaximumOnStringDto)}.name has [SchemaMaximum]");
    }

    [Test]
    public async Task limits_transformer_rejects_a_minimum_on_a_string()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((MinimumOnStringDto dto) => dto);

        // Act
        var act = () => app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain($"{nameof(MinimumOnStringDto)}.name has [SchemaMinimum]");
    }

    [Test]
    public async Task limits_transformer_rejects_a_minimum_more_than_the_maximum()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((MinimumAboveMaximumDto dto) => dto);

        // Act
        var act = () => app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain($"{nameof(MinimumAboveMaximumDto)}.quantity has the minimum 99.5 and the maximum 0.5");
    }

    [Test]
    public async Task limits_transformer_accepts_a_bound_with_an_exponent_from_a_transformer_before_it()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync(
            (BoundsDto dto) => dto, new BoundsTransformer("0", "1.7976931348623157E+308"));

        // Act
        var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var properties = document.Components.ShouldNotBeNull().Schemas.ShouldNotBeNull()[nameof(BoundsDto)].Properties.ShouldNotBeNull();
        properties["weight"].Maximum.ShouldBe("1.7976931348623157E+308");
    }

    [Test]
    public async Task limits_transformer_rejects_a_minimum_with_an_exponent_more_than_the_maximum()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((BoundsDto dto) => dto, new BoundsTransformer("1E+300", "1E+299"));

        // Act
        var act = () => app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain($"{nameof(BoundsDto)}.weight has the minimum 1E+300 and the maximum 1E+299");
    }

    [Test]
    [Arguments("2E-30", "1E-30")]
    [Arguments("1.6E-28", "1.5E-28")]
    public async Task limits_transformer_rejects_a_minimum_more_than_the_maximum_below_the_decimal_range(
        string minimum, string maximum)
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((BoundsDto dto) => dto, new BoundsTransformer(minimum, maximum));

        // Act
        var act = () => app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain($"{nameof(BoundsDto)}.weight has the minimum {minimum} and the maximum {maximum}");
    }

    [Test]
    public async Task limits_transformer_rejects_a_minimum_more_than_the_maximum_by_less_than_a_double_can_hold()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((CloseBoundsDto dto) => dto);

        // Act
        var act = () => app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain($"{nameof(CloseBoundsDto)}.count has the minimum {long.MaxValue} and the maximum {long.MaxValue - 1}");
    }

    [Test]
    [Arguments("-1", "-2")]
    [Arguments("1", "-1")]
    [Arguments("-1.4", "-1.5")]
    [Arguments("1E+5", "-1E+6")]
    [Arguments("1E-1", "1E-2")]
    [Arguments("-1E-2", "-1E-1")]
    [Arguments("10", "9.99")]
    [Arguments("0.0001", "1E-5")]
    [Arguments("0E+5", "-1")]
    [Arguments("1E-9", "-0.0E+3")]
    [Arguments("11111111111111111111111111111111111111111111111112", "11111111111111111111111111111111111111111111111111")]
    [Arguments("2e3", "1e3")]
    [Arguments("1001", "1e3")]
    public async Task limits_transformer_rejects_a_minimum_more_than_the_maximum_in_any_number_form(
        string minimum, string maximum)
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((BoundsDto dto) => dto, new BoundsTransformer(minimum, maximum));

        // Act
        var act = () => app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain($"{nameof(BoundsDto)}.weight has the minimum {minimum} and the maximum {maximum}");
    }

    [Test]
    [Arguments("-2", "-1")]
    [Arguments("-1", "1")]
    [Arguments("-1.5", "-1.4")]
    [Arguments("-2E+3", "-1E+3")]
    [Arguments("1.5", "10")]
    [Arguments("9.99", "10")]
    [Arguments("5", "5")]
    [Arguments("1.0", "1E0")]
    [Arguments("1E0", "1.0")]
    [Arguments("0.1", "1E-1")]
    [Arguments("1E-1", "0.1")]
    [Arguments("0E+5", "-0")]
    [Arguments("-0", "0")]
    [Arguments("-0.0E+3", "0")]
    [Arguments("0", "-0.0E+3")]
    [Arguments("11111111111111111111111111111111111111111111111111", "11111111111111111111111111111111111111111111111112")]
    [Arguments("11111111111111111111111111111111111111111111111111", "11111111111111111111111111111111111111111111111111")]
    [Arguments("1e3", "1000")]
    [Arguments("1000", "1e3")]
    public async Task limits_transformer_accepts_a_minimum_not_more_than_the_maximum_in_any_number_form(
        string minimum, string maximum)
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((BoundsDto dto) => dto, new BoundsTransformer(minimum, maximum));

        // Act
        var document = await app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var weight = document.Components.ShouldNotBeNull().Schemas.ShouldNotBeNull()[nameof(BoundsDto)].Properties.ShouldNotBeNull()["weight"];
        (weight.Minimum, weight.Maximum).ShouldBe((minimum, maximum));
    }

    [Test]
    public async Task limits_transformer_rejects_a_member_that_is_not_a_public_static_number_field()
    {
        // Arrange
        await using var app = await AppWithLimitsTransformerAsync((NonNumericLimitDto dto) => dto);

        // Act
        var act = () => app.Services.GetRequiredKeyedService<IOpenApiDocumentProvider>("v1").GetOpenApiDocumentAsync();

        // Assert
        var exception = await act.ShouldThrowAsync<InvalidOperationException>();
        exception.Message.ShouldContain($"{nameof(Limits)}.{nameof(Limits.Unit)}");
    }

    private static async Task<WebApplication> AppWithLimitsTransformerAsync(
        Delegate handler, IOpenApiSchemaTransformer? transformerBefore = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        // The Server appsettings.json in the test output sets Kestrel endpoints on 8080 and 8081, and they win over UseUrls
        builder.Configuration["Kestrel:Endpoints:http:Url"] = "http://127.0.0.1:0";
        builder.Configuration["Kestrel:Endpoints:grpc:Url"] = "http://127.0.0.1:0";
        // The Server removes the string type from numbers and gives a uint its range first, as SwaggerExtension orders
        // the transformers
        builder.Services.AddOpenApi(options =>
        {
            options.AddSchemaTransformer<RemoveStringFromNumbersTransformer>();
            options.AddSchemaTransformer<UnsignedIntegerRangeTransformer>();
            if (transformerBefore is not null)
                options.AddSchemaTransformer(transformerBefore);
            options.AddSchemaTransformer<SchemaLimitsTransformer>();
        });
        var app = builder.Build();
        app.MapPost("/limited", handler);
        // The app adds its endpoints to the routing data when it starts
        await app.StartAsync();
        return app;
    }

    private static class Limits
    {
        public const int NameMaxLength = 40;
        public static readonly decimal QuantityMin = 0.5m;
        public static readonly decimal QuantityMax = 99.5m;
        public const int SeatsMin = 1;
        public const int VersionMax = 1000;
        public const string Unit = "kg";
        public const long CountMin = long.MaxValue;
        public const long CountMax = long.MaxValue - 1;
    }

    private sealed record LimitedDto
    {
        [SchemaMaxLength(typeof(Limits), nameof(Limits.NameMaxLength))]
        public string Name { get; init; } = default!;
        [SchemaMinimum(typeof(Limits), nameof(Limits.QuantityMin))]
        [SchemaMaximum(typeof(Limits), nameof(Limits.QuantityMax))]
        public decimal Quantity { get; init; }
        [SchemaMaximum(typeof(Limits), nameof(Limits.VersionMax))]
        public uint Version { get; init; }
        [SchemaMinimum(typeof(Limits), nameof(Limits.SeatsMin))]
        public uint Seats { get; init; }
    }

    private sealed record MaxLengthOnNumberDto
    {
        [SchemaMaxLength(typeof(Limits), nameof(Limits.NameMaxLength))]
        public int Quantity { get; init; }
    }

    private sealed record MaximumOnStringDto
    {
        [SchemaMaximum(typeof(Limits), nameof(Limits.QuantityMax))]
        public string Name { get; init; } = default!;
    }

    private sealed record MinimumOnStringDto
    {
        [SchemaMinimum(typeof(Limits), nameof(Limits.SeatsMin))]
        public string Name { get; init; } = default!;
    }

    private sealed record MinimumAboveMaximumDto
    {
        [SchemaMinimum(typeof(Limits), nameof(Limits.QuantityMax))]
        [SchemaMaximum(typeof(Limits), nameof(Limits.QuantityMin))]
        public decimal Quantity { get; init; }
    }

    private sealed record CloseBoundsDto
    {
        [SchemaMinimum(typeof(Limits), nameof(Limits.CountMin))]
        [SchemaMaximum(typeof(Limits), nameof(Limits.CountMax))]
        public long Count { get; init; }
    }

    private sealed record BoundsDto
    {
        public double Weight { get; init; }
    }

    private sealed class BoundsTransformer(string minimum, string maximum) : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
        {
            if (context.JsonPropertyInfo?.DeclaringType == typeof(BoundsDto))
                (schema.Minimum, schema.Maximum) = (minimum, maximum);
            return Task.CompletedTask;
        }
    }

    private sealed record NonNumericLimitDto
    {
        [SchemaMaxLength(typeof(Limits), nameof(Limits.Unit))]
        public string Name { get; init; } = default!;
    }

    private static OpenApiOperationTransformerContext OperationContext(params object[] endpointMetadata) => new()
    {
        DocumentName = "v1",
        Description = new ApiDescription { ActionDescriptor = new ActionDescriptor { EndpointMetadata = endpointMetadata } },
        ApplicationServices = Substitute.For<IServiceProvider>(),
    };

    private static OpenApiOperation Operation(string? operationId, bool paged)
    {
        var response = new OpenApiResponse { Description = "OK" };
        if (paged)
            response.Headers = new Dictionary<string, IOpenApiHeader> { ["X-Pagination"] = new OpenApiHeader() };

        return new OpenApiOperation
        {
            OperationId = operationId,
            Responses = new OpenApiResponses { ["200"] = response },
        };
    }

    private static OpenApiDocument DocumentWith(params (string Path, HttpMethod Method, OpenApiOperation Operation)[] operations)
    {
        var paths = new OpenApiPaths();
        foreach (var group in operations.GroupBy(o => o.Path))
        {
            paths[group.Key] = new OpenApiPathItem
            {
                Operations = group.ToDictionary(o => o.Method, o => o.Operation),
            };
        }

        return new OpenApiDocument { Paths = paths };
    }
}
