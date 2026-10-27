namespace FullstackTemplate.Server.Resources.Entrypoints;

using Domain.Tenants.Grpc.V1;
using Exceptions;
using Grpc;
using Http;
using Http.OpenApi;
using Mcp;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Serilog.Core;
using Services;

/// <summary>
/// Adds and maps the external entrypoints: the HTTP API, gRPC, and MCP. All three use the same JWT bearer
/// authentication, so each one sees the same caller and the same tenant scope. The core services come from
/// <c>ConfigureServices</c>.
/// </summary>
public static class EntrypointRegistration
{
    /// <summary>The path of the MCP streamable HTTP endpoint.</summary>
    public const string McpPath = "/mcp";

    public static void AddEntrypoints(this WebApplicationBuilder builder)
    {
        builder.Services.AddJwtBearerAuthentication(builder.Configuration, builder.Environment);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<ICurrentPrincipalAccessor, HttpContextPrincipalAccessor>();

        // HTTP
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = DomainProblemDetails.Customize);
        builder.Services.AddApiVersioningExtension();
        // A value that does not bind throws, so that DomainProblemDetails gives a 400 with the errors, not an empty 400
        builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        builder.Services.AddSwaggerExtension(builder.Configuration);

        // gRPC
        builder.Services.AddGrpc(options => options.Interceptors.Add<DomainErrorInterceptor>());

        // MCP. The transport is stateless, so each tool call runs in its own HTTP request. Then the
        // HttpContext, the caller, and the tenant scope are the ones of that request.
        builder.Services.AddMcpServer()
            .WithHttpTransport(options => options.Stateless = true)
            .WithToolsFromAssembly(typeof(EntrypointRegistration).Assembly)
            .WithRequestFilters(filters =>
            {
                filters.AddListToolsFilter(McpToolAccess.ListFilter);
                // The first call filter is the outermost, so a denial of the access filter becomes a tool error.
                filters.AddCallToolFilter(McpCallTelemetry.Filter);
                filters.AddCallToolFilter(McpDomainErrors.Filter);
                filters.AddCallToolFilter(McpToolAccess.CallFilter);
            });
        // ReadFrom.Services in HostExtensions adds each ILogEventFilter of the services to the logger
        builder.Services.AddSingleton<ILogEventFilter, McpProtocolErrorLogFilter>();
    }

    /// <summary>
    /// Adds the middleware of the entrypoints: the problem details of HTTP errors, the OpenAPI document in
    /// Development, and authentication.
    /// </summary>
    public static void UseEntrypoints(this WebApplication app)
    {
        app.UseExceptionHandler(new ExceptionHandlerOptions
        {
            StatusCodeSelector = DomainProblemDetails.StatusCodeFor,
            // A client error is an expected outcome, so it is not logged as an unhandled exception.
            SuppressDiagnosticsCallback = context => DomainProblemDetails.IsClientError(context.Exception),
        });
        // Routing checks the request content type, so the text/json rewrite must run before routing
        app.UseMiddleware<JsonMediaTypeMiddleware>();
        app.UseRouting();

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.UseSwaggerExtension(app.Configuration);
        }

        app.UseAuthentication();
        app.UseAuthorization();
    }

    public static void MapEntrypoints(this WebApplication app)
    {
        app.MapVersionedEndpoints();
        app.MapGrpcService<TenantsGrpcService>();
        McpToolAccess.Validate(app.Services.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection ?? []);
        app.MapMcp(McpPath).RequireAuthorization();
    }
}
