namespace FullstackTemplate.Server.Resources.Entrypoints.DPoP;

using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;
using Serilog;
using ILogger = Serilog.ILogger;

/// <summary>
/// DPoP (RFC 9449) on top of the Microsoft JWT bearer handler. The handler validates the access token. This code
/// adds three things: it reads a token from the <c>DPoP</c> authorization scheme, it checks the DPoP proof of such a
/// request, and it rejects a DPoP-bound token that comes as a Bearer token (section 7.2). A failure gives a
/// <c>WWW-Authenticate: DPoP</c> challenge (section 7.1).
/// </summary>
public static class DPoPAuthentication
{
    public const string SchemeName = "DPoP";
    public const string ProofHeaderName = "DPoP";

    private const string SchemeItemKey = "FullstackTemplate.DPoP.Scheme";
    private const string TokenItemKey = "FullstackTemplate.DPoP.Token";

    private static readonly ILogger Logger = Log.ForContext(typeof(DPoPAuthentication));

    /// <summary>
    /// Adds the options, the proof validator, the replay cache with its cleanup, and the DPoP handling of the
    /// JWT bearer handler. Call it after <c>AddJwtBearer</c>.
    /// </summary>
    public static IServiceCollection AddDPoP(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<DPoPOptions>(configuration.GetSection(DPoPOptions.SectionName));
        services.AddScoped<IDPoPReplayCache, DPoPReplayCache>();
        services.AddScoped<DPoPProofValidator>();
        services.AddHostedService<DPoPReplayCacheCleanupService>();
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, UseDPoP);
        return services;
    }

    /// <summary>
    /// Wraps the events of the JWT bearer handler. The events that the options already have still run.
    /// </summary>
    public static void UseDPoP(JwtBearerOptions options)
    {
        var events = options.Events;
        var messageReceived = events.OnMessageReceived;
        var tokenValidated = events.OnTokenValidated;
        var challenge = events.OnChallenge;

        events.OnMessageReceived = async context =>
        {
            await messageReceived(context);
            ReadDPoPToken(context);
        };

        events.OnTokenValidated = async context =>
        {
            await tokenValidated(context);
            if (context.Result is null)
                await CheckBindingAsync(context);
        };

        events.OnChallenge = async context =>
        {
            await challenge(context);
            if (!context.Handled)
                WriteChallenge(context);
        };
    }

    private static void ReadDPoPToken(MessageReceivedContext context)
    {
        if (!string.IsNullOrEmpty(context.Token))
            return;

        var header = context.Request.Headers.Authorization.ToString();
        if (!header.StartsWith(SchemeName + " ", StringComparison.OrdinalIgnoreCase))
            return;

        var token = header[(SchemeName.Length + 1)..].Trim();
        if (token.Length == 0)
            return;

        context.Token = token;
        context.HttpContext.Items[SchemeItemKey] = SchemeName;
        context.HttpContext.Items[TokenItemKey] = token;
    }

    private static async Task CheckBindingAsync(TokenValidatedContext context)
    {
        var httpContext = context.HttpContext;
        var usedDPoPScheme = httpContext.Items.ContainsKey(SchemeItemKey);
        var boundJkt = BoundKeyThumbprint(context.SecurityToken as JsonWebToken);

        if (!usedDPoPScheme)
        {
            // Section 7.2: a DPoP-bound token must not work as a bearer token
            if (boundJkt is not null)
                Fail(context, DPoPValidationResult.Token("A DPoP-bound access token must use the DPoP authentication scheme."));
            return;
        }

        if (boundJkt is null)
        {
            Fail(context, DPoPValidationResult.Token("The access token is not bound to a DPoP key."));
            return;
        }

        var proofs = httpContext.Request.Headers[ProofHeaderName];
        if (proofs.Count != 1)
        {
            Fail(context, DPoPValidationResult.Proof(proofs.Count == 0
                ? "The DPoP header is missing."
                : "There must be one DPoP header."));
            return;
        }

        var request = httpContext.Request;
        var url = new Uri(UriHelper.BuildAbsolute(request.Scheme, request.Host, request.PathBase, request.Path));
        var validator = httpContext.RequestServices.GetRequiredService<DPoPProofValidator>();
        var result = await validator.ValidateAsync(
            proofs[0]!, new DPoPRequest(request.Method, url, (string)httpContext.Items[TokenItemKey]!, boundJkt),
            httpContext.RequestAborted);

        if (!result.Succeeded)
        {
            Fail(context, result);
            return;
        }

        Activity.Current?.SetTag("app.auth.scheme", SchemeName);
        Activity.Current?.SetTag("app.dpop.jkt_prefix", result.Jkt![..8]);
    }

    private static void Fail(TokenValidatedContext context, DPoPValidationResult result)
    {
        Logger.Here().Information("Rejected a request to {Path} with {Error}: {Description}",
            context.Request.Path.Value, result.Error, result.Description);
        context.Fail(new DPoPAuthenticationException(result));
    }

    /// <summary>
    /// The <c>cnf.jkt</c> claim of the access token (RFC 9449 section 6.1), or null when the token is not DPoP-bound.
    /// </summary>
    public static string? BoundKeyThumbprint(JsonWebToken? token) =>
        token is not null
        && DPoPProofValidator.PayloadOf(token).TryGetProperty("cnf", out var cnf)
        && cnf.ValueKind == JsonValueKind.Object
        && cnf.TryGetProperty("jkt", out var jkt)
        && jkt.ValueKind == JsonValueKind.String
            ? jkt.GetString()
            : null;

    private static void WriteChallenge(JwtBearerChallengeContext context)
    {
        var algs = string.Join(' ', DPoPProofValidator.AllowedAlgorithms);
        var usedDPoPScheme = context.HttpContext.Items.ContainsKey(SchemeItemKey);

        if (context.AuthenticateFailure is DPoPAuthenticationException failure)
        {
            Respond(context, failure.Result.Error!, failure.Result.Description, algs);
        }
        else if (usedDPoPScheme && context.AuthenticateFailure is not null)
        {
            Respond(context, DPoPValidationResult.InvalidToken, "The access token is not valid.", algs);
        }
        else
        {
            // No credentials, or a Bearer failure: add the DPoP challenge next to the Bearer challenge of the handler
            context.Response.Headers.Append(HeaderNames.WWWAuthenticate, $"{SchemeName} algs=\"{algs}\"");
        }
    }

    private static void Respond(JwtBearerChallengeContext context, string error, string? description, string algs)
    {
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        var value = $"{SchemeName} error=\"{error}\"";
        if (description is not null)
            value += $", error_description=\"{description.Replace("\"", "'")}\"";
        context.Response.Headers.Append(HeaderNames.WWWAuthenticate, $"{value}, algs=\"{algs}\"");
    }
}

/// <summary>
/// A failed DPoP check. The JWT bearer handler passes it to the challenge as the authentication failure.
/// </summary>
public sealed class DPoPAuthenticationException(DPoPValidationResult result) : Exception(result.Description)
{
    public DPoPValidationResult Result { get; } = result;
}

/// <summary>
/// Deletes the expired rows of the replay cache of the DPoP proofs in batches, each
/// <c>Auth:DPoP:CleanupInterval</c>. Each server instance runs it. The delete is idempotent, so the instances need no lock.
/// </summary>
public sealed class DPoPReplayCacheCleanupService(
    IServiceScopeFactory scopeFactory,
    IOptions<DPoPOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    public const int BatchSize = 500;

    private static readonly ILogger Logger = Log.ForContext<DPoPReplayCacheCleanupService>();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.CleanupInterval, timeProvider);
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await CleanupAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // The next run tries again. An exception here would stop the host.
                    Logger.Here().Error(ex, "The cleanup of the DPoP replay cache failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // A normal stop
        }
    }

    /// <summary>
    /// Deletes all the rows that expired, and returns their number.
    /// </summary>
    public async Task<int> CleanupAsync(CancellationToken cancellationToken)
    {
        // The cleanup has no request, so it starts its own trace.
        using var trace = Telemetry.TraceRoot([], "dpop.replay_cache.cleanup");
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var cache = scope.ServiceProvider.GetRequiredService<IDPoPReplayCache>();
            var now = timeProvider.GetUtcNow();

            var total = 0;
            int deleted;
            do
            {
                deleted = await cache.DeleteExpiredAsync(now, BatchSize, cancellationToken);
                total += deleted;
            }
            while (deleted == BatchSize);

            if (total > 0)
                Logger.Here().Information("Deleted {Count} expired proofs from the DPoP replay cache", total);
            return total;
        }
        catch (Exception ex)
        {
            Telemetry.RecordError(trace.Activity, ex);
            throw;
        }
    }
}
