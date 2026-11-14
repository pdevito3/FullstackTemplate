namespace FullstackTemplate.Server.Resources.Entrypoints.Jobs;

using BackWave.Dashboard;
using Domain.Users;
using Resources.Jobs;

/// <summary>
/// The BackWave dashboard at <see cref="Path"/>, for the users in <c>BackgroundJobs:OperatorSubjects</c> only. The
/// BFF forwards the browser to it with the access token of the session.
/// </summary>
public static class JobsDashboard
{
    public const string Path = "/backwave";

    /// <summary>The antiforgery cookie of the dashboard forms. The browser sends it only to the dashboard.</summary>
    public const string AntiforgeryCookieName = "backwave-antiforgery";

    /// <summary>
    /// Adds the antiforgery tokens of the write actions and the live metrics panel. The keys of the tokens are in the
    /// key ring of the core.
    /// </summary>
    public static void AddJobsDashboard(this IServiceCollection services)
    {
        services.AddAntiforgery(options =>
        {
            options.Cookie.Name = AntiforgeryCookieName;
            options.Cookie.Path = Path;
            options.Cookie.SameSite = SameSiteMode.Strict;
            // The BFF can call the Server over http. With Always, antiforgery throws on such a request.
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        });
        // The panel shows the jobs of this process only. A process with the Api role runs no job.
        services.AddBackWaveDashboardMetrics();
    }

    /// <summary>Mounts the dashboard. Call it after <c>UseAuthorization</c>, so that the caller is the token.</summary>
    public static void UseJobsDashboard(this IApplicationBuilder app) =>
        app.UseBackWaveDashboard(Path, new BackWaveDashboardOptions
        {
            AuthorizeView = IsOperatorAsync,
            AuthorizeViewSensitiveData = IsOperatorAsync,
            AuthorizeRequeue = IsOperatorAsync,
            AuthorizeCancel = IsOperatorAsync,
            // AuthorizePauseQueue and AuthorizeTriggerSchedule keep the default deny. The app has no schedule, and a
            // pause holds the jobs of all tenants.
            // BackWave resolves the actor only after the check of the write passes, and an operator is a user.
            ResolveActor = context => context.RequestServices.GetRequiredService<ICurrentUserService>().UserIdentifier
                ?? throw new InvalidOperationException("A write of the jobs dashboard passed the operator check with no user."),
        });

    private static async ValueTask<bool> IsOperatorAsync(HttpContext context)
        => await context.RequestServices.GetRequiredService<JobOperators>().IncludesCallerAsync(context.RequestAborted);
}
