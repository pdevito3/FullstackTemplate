namespace FullstackTemplate.Server.Resources.Entrypoints.Jobs;

using BackWave.Pro.Mcp;
using Domain.Users;

/// <summary>
/// The BackWave tools on the MCP endpoint of the app. <see cref="Mcp.McpToolAccess"/> shows them only to a caller
/// that manages all tenants, because they see the jobs of all tenants.
/// </summary>
public static class JobsMcp
{
    /// <summary>Sets the gates of BackWave.</summary>
    public static void Configure(BackWaveProMcpOptions options)
    {
        // The view gate of BackWave covers the whole MCP server, also the tools of the app, so it allows each request.
        // McpToolAccess gates the BackWave tools.
        options.AuthorizeView = _ => ValueTask.FromResult(true);
        options.AuthorizeRequeue = ManagesAllTenantsAsync;
        options.AuthorizeCancel = ManagesAllTenantsAsync;
        // AuthorizePauseQueue, AuthorizeTriggerSchedule, and AuthorizeSetConcurrencyLimit keep the default deny. A pause
        // or a low concurrency limit holds the jobs of all tenants, and each run of the one schedule, the sweep of the
        // bucket, lists the whole bucket.
        // AuthorizeViewSensitiveData keeps the default deny, so that no payload or output goes into the context of a
        // model. The message of the exception that failed a job is not gated.
        options.ResolveActor = context =>
            context.RequestServices.GetRequiredService<ICallerIdentityResolver>().Current switch
            {
                CallerIdentity.AllTenantsClient { CanManage: true, ClientId: var clientId } => $"client:{clientId}",
                _ => throw new InvalidOperationException(
                    "A BackWave write on MCP passed the check with no client that manages all tenants."),
            };
    }

    private static async ValueTask<bool> ManagesAllTenantsAsync(HttpContext context)
        => (await context.RequestServices.GetRequiredService<IPermissionGuard>().GetCallerAsync(context.RequestAborted))
            .ManagesAllTenants;
}
