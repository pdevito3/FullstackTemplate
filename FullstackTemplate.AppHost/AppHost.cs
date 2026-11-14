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

    // A normal run uses the fixed ports of the port table in README.md. `aspire run --isolated` gives each proxied
    // endpoint a random port and skips the data volumes, so that worktrees can run the full stack at the same time.
    // See Isolation.cs.
    var postgres = builder.AddPostgres("postgres", port: 24720)
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

    // S3 object storage. Floci emulates S3 in a local run only. A published server gets the S3 of its cloud and its
    // bucket from the deploy, not an emulator with test credentials. The browser loads the presigned URLs of the
    // server from this endpoint, so it must be reachable from the browser. The bucket hook in Storage/seed creates
    // the bucket.
    if (builder.ExecutionContext.IsRunMode)
    {
        const string storageBucket = "files";
        var storage = builder.AddFlociAws("storage", port: 24780)
            .WithImageTag(Config.FlociImageTag)
            .WithEnvironment(Config.FlociValidateSignaturesVariable, "true")
            .WithEnvironment("STORAGE_BUCKET", storageBucket)
            .WithBindMount("./Storage/seed", "/etc/floci/init/ready.d", isReadOnly: true)
            .WithDataVolumeUnlessIsolated();
        // A path-style GET of the bucket gives 404 until the hook created it
        storage.WithHttpHealthCheck($"/{storageBucket}", endpointName: storage.Resource.PrimaryEndpoint.EndpointName);
        server.WithReference(storage)
            .WaitFor(storage)
            .WithEnvironment("ObjectStorage__BucketName", storageBucket);
        storage.WithParentRelationship(server);
    }

    // MCP clients call the server directly, so the resource of its protected resource metadata is the URL they call.
    server.WithEnvironment("Mcp__PublicOrigin", server.GetEndpoint("https"));

    var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
        .WithEndpoint("http", endpoint => endpoint.Port = 24710)
        .WithPnpm();

    var bff = builder.AddProject<Projects.FullstackTemplate_Bff>("bff")
        .WithReference(bffDb)
        .WaitFor(bffDb)
        .WithReference(server)
        .WithHttpHealthCheck("/health")
        .WithExternalHttpEndpoints()
        .WithParentRelationship(webfrontend);
    // services:webfrontend tells the BFF that the Vite dev server serves the SPA. A published BFF serves the SPA
    // from wwwroot, so it must not get the reference.
    if (builder.ExecutionContext.IsRunMode)
    {
        if (builder.Configuration["SpaBuild"] is { Length: > 0 } spaBuild)
            bff.WithEnvironment("ASPNETCORE_WEBROOT", Path.GetFullPath(spaBuild));
        else
            bff.WithReference(webfrontend);
    }
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
        webfrontend.GetEndpoint("http"), server.GetEndpoint("https"), server.GetEndpoint("http"), bff.GetEndpoint("https"),
        bffForContainers);
    // The AuthProvider setting selects the provider. appsettings.json gives the default, and the environment
    // variable AuthProvider=Keycloak overrides it. The e2e tests find the provider from the running stack.
    var selectedProvider = AuthProviderCatalog.Select(builder.Configuration);
    var authProvider = selectedProvider.AddTo(builder, authClientUrls);
    // DPoP=true runs the stack with DPoP (RFC 9449): the BFF binds the tokens to its key. The setting is a flag like
    // AuthProvider, for example the environment variable DPoP=true.
    if (AuthProviderDPoP.IsOn(builder))
        authProvider = authProvider.WithDPoP(selectedProvider, builder);
    server.WithServerAuth(authProvider);
    // In a local run, the admin user of the seed can open the jobs dashboard at /backwave. A published server gets
    // its operators from its own BackgroundJobs:OperatorSubjects setting.
    if (builder.ExecutionContext.IsRunMode && authProvider.OperatorSubject is { } operatorSubject)
        server.WithEnvironment("BackgroundJobs__OperatorSubjects__0", operatorSubject);
    bff.WithBffAuth(authProvider);
    authProvider.ConfigureBff?.Invoke(bff);

    // Only the e2e runs set it. A margin longer than the access token lifetime makes each API call refresh, so
    // that the e2e tests prove that the BFF refreshes and stores the rotated tokens.
    if (builder.Configuration["BffRefreshBeforeExpirationSeconds"] is { Length: > 0 } refreshMargin)
    {
        bff.WithEnvironment("Auth__RefreshBeforeExpirationSeconds", refreshMargin);
    }

    // The UI starts only when a login and an API call can work. The BFF waits for the auth provider.
    webfrontend.WithReference(bff)
        .WaitFor(bff)
        .WaitFor(server);

    // The BFF serves the SPA in production. The server is an internal API only.
    bff.PublishWithContainerFiles(webfrontend, "wwwroot");

    // Labcoat finds the test projects of the solution and shows a web UI to run them. Aspire tests and publish skip it.
    builder.AddLabcoat("tests")
        .WithHostPort(24705);

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
