namespace FullstackTemplate.Server.Resources;

/// <summary>
/// CSharpRepl runs any C# code in the process, so it is for local development only (see docs/csharp-repl.md).
/// The runtime loads the CSharpRepl startup hook before <c>Main</c>. This guard stops the server
/// when the hook is loaded and the environment is not Development.
/// </summary>
public static class CSharpReplGuard
{
    private const string HookAssemblyPrefix = "CSharpRepl.InjectedHook";

    public static void ThrowIfLoadedOutsideDevelopment(IHostEnvironment environment)
    {
        if (environment.IsDevelopment())
            return;

        var hookIsLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .Any(assembly => assembly.GetName().Name?.StartsWith(HookAssemblyPrefix, StringComparison.Ordinal) == true);

        if (hookIsLoaded)
            throw new InvalidOperationException(
                $"The CSharpRepl startup hook is loaded in the '{environment.EnvironmentName}' environment. " +
                "CSharpRepl is for Development only. Remove DOTNET_STARTUP_HOOKS and ASPNETCORE_HOSTINGSTARTUPASSEMBLIES.");
    }
}
