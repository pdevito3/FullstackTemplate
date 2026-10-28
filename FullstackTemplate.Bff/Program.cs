using System.Globalization;
using FullstackTemplate.Bff;
using FullstackTemplate.Bff.ApiEndpoints;
using FullstackTemplate.Bff.Authentication;
using FullstackTemplate.Bff.HostTrust;
using FullstackTemplate.Bff.Login;
using FullstackTemplate.Bff.Management;
using FullstackTemplate.Bff.Proxy;
using FullstackTemplate.Bff.Sessions;
using Serilog;
using Serilog.Events;
using Yarp.ReverseProxy.Forwarder;

// Bootstrap logger to catch startup errors before host is built
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(formatProvider: CultureInfo.InvariantCulture)
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting FullstackTemplate.Bff");

    var builder = WebApplication.CreateBuilder(args);

    builder.AddServiceDefaults(); // OpenTelemetry, Serilog, health checks, resilience, service discovery
    builder.Services.AddProblemDetails();

    // Before the authentication, so that the bffdb migrations run before the Data Protection key ring loads
    builder.AddBffSessions();
    builder.Services.AddBffAuthentication(builder.Configuration, builder.Environment);
    builder.Services.AddHttpForwarder();
    // After AddHttpForwarder, which registers the default factory. The wrapper adds the DPoP nonce retry.
    builder.Services.AddSingleton<IForwarderHttpClientFactory, DPoPProxy.NonceRetryClientFactory>();
    // Bff:PublicOrigin, the forwarded headers, and the host filter
    builder.Services.AddHostTrust();
    builder.Services.AddSingleton(provider => SpaOrigin.FromConfiguration(provider.GetRequiredService<IConfiguration>()));

    var app = builder.Build();

    // The host filter of the web host defaults runs before this, with the AllowedHosts of AddHostTrust.
    // First, so that the request logs and the handlers see the client address and the scheme from the proxy
    app.UseForwardedHeaders();
    // The OIDC handler builds the redirect URIs from the public origin, never from the request
    app.UsePublicOrigin();

    app.UseSerilogRequestLogging(options =>
    {
        options.GetLevel = Extensions.RequestLogLevel;
        // The Serilog default, with the browser path cleaned of CR, LF, and the other control characters
        options.GetMessageTemplateProperties = (httpContext, requestPath, elapsed, statusCode) =>
        [
            new LogEventProperty("RequestMethod", new ScalarValue(httpContext.Request.Method)),
            new LogEventProperty("RequestPath", new ScalarValue(LogValue.Clean(requestPath))),
            new LogEventProperty("StatusCode", new ScalarValue(statusCode)),
            new LogEventProperty("Elapsed", new ScalarValue(elapsed)),
        ];
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            diagnosticContext.Set("RequestHost", LogValue.Clean(httpContext.Request.Host.Value));
            diagnosticContext.Set("RequestScheme", httpContext.Request.Scheme);
            diagnosticContext.Set("UserAgent", LogValue.Clean(httpContext.Request.Headers.UserAgent.ToString()));
        };
    });

    app.UseExceptionHandler();

    // In development the Vite dev server serves the SPA. Otherwise the SPA build is in wwwroot.
    var spaOrigin = app.Services.GetRequiredService<SpaOrigin>();

    // The SPA fallback skips paths with a file extension, so the JS and CSS bundles need this
    if (spaOrigin.DevServer is null)
    {
        app.UseStaticFiles();
    }

    app.UseCallbackMethodGuard();
    // Also answers the OIDC callbacks, /signin-oidc and /signout-callback-oidc
    app.UseAuthentication();
    app.UseRouting();
    // After routing, because it reads the endpoint metadata
    app.UseCsrfHeaderCheck();
    app.UseAuthorization();

    app.MapBffManagementEndpoints();

    app.MapGet("/local-api/hello", () => new { message = "Hello from BFF!", timestamp = DateTime.UtcNow })
        .AsBffApiEndpoint();

    var serverUrl = builder.Configuration["services:server:https:0"]
        ?? builder.Configuration["services:server:http:0"]
        ?? "https://localhost:5001";

    // The proxy strips the local path prefix when forwarding, so include /api in the remote URL
    var apiBaseUrl = new Uri(new Uri(serverUrl.TrimEnd('/') + "/"), "api");
    app.MapApiProxy(apiBaseUrl);

    app.MapDefaultEndpoints();

    // A middleware after UseAuthorization, so that it sees the result of routing. A routed fallback endpoint would
    // match each method, and routing would then pick it over the 405 of a BFF endpoint.
    app.UseSpaFallback(spaOrigin);

    app.Run();
}
// WebApplicationFactory stops the host on purpose with a HostAbortedException.
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
    // A failed startup, such as a failed discovery read, must not look like a clean stop. The rethrow gives a
    // non-zero exit code, and it gives the cause to the test host.
    throw;
}
finally
{
    await Log.CloseAndFlushAsync();
}
