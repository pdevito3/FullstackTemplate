namespace FullstackTemplate.AppHost;

/// <summary>
/// Runs the Server as two processes, when the AppHost setting <c>SplitServer</c> is true: <c>server</c> with the role
/// Api and <c>server-worker</c> with the role Worker. A local run then has the split of a deploy. See the roles in
/// docs/backend/background-jobs.md.
/// </summary>
public static class SplitServer
{
    public const string SettingName = "SplitServer";

    public const string WorkerName = "server-worker";

    /// <summary>
    /// Whether the AppHost setting <c>SplitServer</c> is true, for example the environment variable
    /// <c>SplitServer=true</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The setting is true in a publish.</exception>
    public static bool IsOn(IDistributedApplicationBuilder builder)
    {
        if (!bool.TryParse(builder.Configuration[SettingName], out var on) || !on)
            return false;

        // A deploy picks its own processes, so a publish keeps one Server
        if (builder.ExecutionContext.IsPublishMode)
            throw new InvalidOperationException(
                $"{SettingName}=true is for a local run only. A deploy sets Server__Role on each Server process.");

        return true;
    }
}
