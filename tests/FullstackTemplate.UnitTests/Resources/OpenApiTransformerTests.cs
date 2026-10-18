namespace FullstackTemplate.UnitTests.Resources;

using FullstackTemplate.Server.Resources.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using NSubstitute;
using Shouldly;

public class OpenApiTransformerTests
{
    // The transformers read only the document, so the context needs no real values.
    private static readonly OpenApiDocumentTransformerContext Context = new()
    {
        DocumentName = "v1",
        DescriptionGroups = [],
        ApplicationServices = Substitute.For<IServiceProvider>(),
    };

    [Fact]
    public async Task operation_id_transformer_accepts_named_operations()
    {
        // Arrange
        var document = DocumentWith(("/api/v1/tenants/{id}", HttpMethod.Get, Operation("GetTenant", paged: false)));

        // Act
        var act = () => new RequireOperationIdTransformer().TransformAsync(document, Context, CancellationToken.None);

        // Assert
        await act.ShouldNotThrowAsync();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
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
