namespace FullstackTemplate.Server.Resources.Extensions;

/// <summary>
/// The work of one Server process. Set it with <c>Server:Role</c> (env <c>Server__Role</c>). With no value, the role is
/// <see cref="All"/>. A deploy can run the same image as <see cref="Api"/> processes and <see cref="Worker"/> processes,
/// to scale the requests and the jobs apart.
/// </summary>
public enum ServerRole
{
    /// <summary>Serves requests and runs jobs.</summary>
    All,

    /// <summary>Serves requests and queues jobs, but runs no job.</summary>
    Api,

    /// <summary>Runs jobs. It serves only the health probes.</summary>
    Worker,
}

public static class ServerRoles
{
    public const string ConfigurationKey = "Server:Role";

    /// <summary>The role in the configuration. Read it one time, in <c>Program</c>.</summary>
    /// <exception cref="InvalidOperationException">The value is not the name of a role.</exception>
    public static ServerRole From(IConfiguration configuration)
    {
        var value = configuration[ConfigurationKey];
        // A switch on the names, not Enum.Parse, so that "1" is not a role
        return value?.Trim().ToUpperInvariant() switch
        {
            null or "" or "ALL" => ServerRole.All,
            "API" => ServerRole.Api,
            "WORKER" => ServerRole.Worker,
            _ => throw new InvalidOperationException($"{ConfigurationKey} is '{value}'. Use All, Api, or Worker."),
        };
    }

    /// <summary>True when the role maps the HTTP API, gRPC, MCP, and the jobs dashboard.</summary>
    public static bool ServesRequests(this ServerRole role) => role switch
    {
        ServerRole.All or ServerRole.Api => true,
        ServerRole.Worker => false,
    };

    /// <summary>True when the role runs the worker groups, which take the jobs from the store.</summary>
    public static bool RunsJobs(this ServerRole role) => role switch
    {
        ServerRole.All or ServerRole.Worker => true,
        ServerRole.Api => false,
    };
}
