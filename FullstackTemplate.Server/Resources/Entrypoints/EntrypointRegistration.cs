namespace FullstackTemplate.Server.Resources.Entrypoints;

using Domain.Tenants.Grpc.V1;
// new-feature:grpc-usings
using Domain.Users;
using Grpc;
using Http;
using Http.OpenApi;
using Jobs;
using Mcp;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using Serilog.Core;

/// <summary>
/// Adds and maps the external entrypoints: the HTTP API, gRPC, MCP, and the jobs dashboard. All of them use the same
/// JWT bearer authentication, so each one sees the same caller and the same tenant scope. The core services come from
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
        builder.Services.Replace(ServiceDescriptor.Scoped<ICurrentPrincipalAccessor, HttpContextPrincipalAccessor>());

        // HTTP
        builder.Services.AddProblemDetails(options => options.CustomizeProblemDetails = DomainProblemDetails.Customize);
        builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.TypeInfoResolverChain.Insert(0, EntrypointJsonContext.Default));
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
            .WithToolsFromAssembly(typeof(EntrypointRegistration).Assembly);
        builder.Services.Configure<McpServerOptions>(options =>
        {
            // AddMcp of BackWave (JobsMcp) names the whole server BackWave. With no name, the SDK gives the name and
            // version of the app.
            options.ServerInfo = null;
            // The first filter is the outermost. AddMcp of BackWave also adds filters, so the filters of the app go
            // before them: each call gets the telemetry and the tool errors of the app, and a denial of the access
            // filter becomes a permission_denied tool error.
            var filters = options.Filters.Request;
            filters.ListToolsFilters.Insert(0, McpToolAccess.ListFilter);
            filters.CallToolFilters.Insert(0, McpToolAccess.CallFilter);
            filters.CallToolFilters.Insert(0, McpDomainErrors.Filter);
            filters.CallToolFilters.Insert(0, McpCallTelemetry.Filter);
        });
        // ReadFrom.Services in HostExtensions adds each ILogEventFilter of the services to the logger
        builder.Services.AddSingleton<ILogEventFilter, McpProtocolErrorLogFilter>();

        builder.Services.AddJobsDashboard();
    }

    /// <summary>
    /// Adds the middleware of the entrypoints: the problem details of HTTP errors, the OpenAPI document in
    /// Development, authentication, and the jobs dashboard.
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
        app.UseJobsDashboard();
    }

    public static void MapEntrypoints(this WebApplication app)
    {
        app.MapVersionedEndpoints();
        app.MapGrpcService<TenantsGrpcService>();
        // new-feature:grpc-services
        var mcpTools = app.Services.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection ?? [];
        McpToolAccess.Validate(mcpTools);
        McpToolHints.Validate(mcpTools);
        app.MapMcp(McpPath).RequireAuthorization();
        app.MapMcpResourceMetadata();
    }
}
