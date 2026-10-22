using System.Globalization;
using FullstackTemplate.AppHost;
using FullstackTemplate.AppHost.AuthProviders;
using Serilog;
using Serilog.Events;

// Bootstrap logger to catch startup errors before host is built
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Aspire.Hosting.Dcp", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting FullstackTemplate.AppHost");

    var builder = DistributedApplication.CreateBuilder(args);

    builder.Services.AddSerilog((_, lc) => lc
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .Enrich.WithThreadId()
        .Enrich.WithProperty("Application", "FullstackTemplate.AppHost"));

    // `aspire run --isolated` gives each proxied endpoint a random port and skips the data volumes,
    // so that worktrees can run the full stack at the same time. See Isolation.cs.
    var postgres = builder.AddPostgres("postgres")
        .WithImageTag(Config.PostgresImageTag)
        .WithDataVolumeUnlessIsolated();
    var appDb = postgres.AddDatabase("appdb");
    // The BFF sessions and the Data Protection keys of the BFF
    var bffDb = postgres.AddDatabase("bffdb");

    var server = builder.AddProject<Projects.FullstackTemplate_Server>("server")
        .WithReference(appDb)
        .WaitFor(appDb)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        // The endpoints come from Kestrel:Endpoints in the appsettings of the Server, with the HTTP/2-only "grpc"
        // endpoint. Aspire makes one endpoint for each name and sets its Url.
        .WithCSharpRepl();
    postgres.WithParentRelationship(server);

    // The proxy keeps 5173 in a normal run. An isolated run replaces it with a random port.
    var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
        .WithEndpoint("http", endpoint => endpoint.Port = 5173)
        .WithPnpm();

    var bff = builder.AddProject<Projects.FullstackTemplate_Bff>("bff")
        .WithReference(bffDb)
        .WaitFor(bffDb)
        .WithReference(server)
        .WithReference(webfrontend)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithParentRelationship(webfrontend);
    // The identity provider containers call /bff/backchannel through this endpoint. The Aspire container tunnel
    // gives it its own host and port. With the tunnel off, the host is host.docker.internal.
    var bffForContainers = bff.GetEndpoint("http", KnownNetworkIdentifiers.DefaultAspireContainerNetwork);
    // The BFF builds its redirect URIs from its own URL, because the Vite proxy changes the origin.
    bff.WithEnvironment("Bff__PublicOrigin", bff.GetEndpoint("https"));
    // In a local run, the host filter also lets in the identity provider containers, through the container endpoint
    // above or host.docker.internal, and the health probes, which call localhost. A published BFF does not trust
    // these hosts.
    if (builder.ExecutionContext.IsRunMode)
        bff.WithEnvironment("Bff__AdditionalAllowedHosts", ReferenceExpression.Create(
            $"{bffForContainers.Property(EndpointProperty.Host)};host.docker.internal;localhost"));

    var authClientUrls = new AuthClientUrls(
        webfrontend.GetEndpoint("http"), server.GetEndpoint("https"), bff.GetEndpoint("https"), bffForContainers);
    // The AuthProvider setting selects the provider. appsettings.json gives the default, and the environment
    // variable AuthProvider=Keycloak overrides it. The e2e tests find the provider from the running stack.
    var selectedProvider = AuthProviderCatalog.Select(builder.Configuration);
    var authProvider = selectedProvider.AddTo(builder, authClientUrls);
    // DPoP=true runs the stack with DPoP (RFC 9449): the BFF binds the tokens to its key. The setting is a flag like
    // AuthProvider, for example the environment variable DPoP=true.
    if (AuthProviderDPoP.IsOn(builder))
        authProvider = authProvider.WithDPoP(selectedProvider, builder);
    server.WithServerAuth(authProvider);
    bff.WithBffAuth(authProvider);
    authProvider.ConfigureBff?.Invoke(bff);

    // Only the e2e runs set it. A margin longer than the access token lifetime makes each API call refresh, so
    // that the e2e tests prove that the BFF refreshes and stores the rotated tokens.
    if (builder.Configuration["BffRefreshBeforeExpirationSeconds"] is { Length: > 0 } refreshMargin)
    {
        bff.WithEnvironment("Auth__RefreshBeforeExpirationSeconds", refreshMargin);
    }

    webfrontend.WithReference(bff);

    // The BFF serves the SPA in production. The server is an internal API only.
    bff.PublishWithContainerFiles(webfrontend, "wwwroot");

    // Labcoat finds the test projects of the solution and shows a web UI to run them. Aspire tests and publish skip it.
    builder.AddLabcoat("tests");

    builder.Build().Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "AppHost terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
