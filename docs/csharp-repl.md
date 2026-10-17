# CSharpRepl

[CSharpRepl](https://github.com/waf/CSharpRepl) connects to the running server and runs C# in it. The code uses the live DI container, database, and configuration. You can inspect state, send a MediatR command or query without HTTP, simulate a user, and wrap or replace a running method.

CSharpRepl is for local development only. It runs any C# code in the server process.

The agent procedure is in the `csharprepl` skill (`.agents/skills/csharprepl/SKILL.md`).

## How it is wired

CSharpRepl loads into the server as a .NET startup hook. The hook opens a local socket, and the CLI connects to this socket.

| Part | File | What it does |
|------|------|--------------|
| Tool version | `dotnet-tools.json` | Pins `csharprepl` as a local .NET tool |
| AppHost | `FullstackTemplate.AppHost/CSharpRepl.cs` | `WithCSharpRepl()` sets `DOTNET_STARTUP_HOOKS` and `ASPNETCORE_HOSTINGSTARTUPASSEMBLIES` on the `server` resource |
| Release builds | `FullstackTemplate.Server/FullstackTemplate.Server.csproj` | `StartupHookSupport` is `false` for the `Release` configuration |
| Server guard | `FullstackTemplate.Server/Resources/CSharpReplGuard.cs` | Stops the server if the hook is loaded outside `Development` |

`WithCSharpRepl()` runs `dotnet tool run csharprepl connect init` to get the path of the hook. If the tool is not installed, the AppHost logs a warning and starts the server without the hook.

Only the `server` resource gets the hook. The `dotnet run` process that starts the server inherits the variables, so `connect list` also shows a `dotnet` row.

## Development only

Three guards keep CSharpRepl out of a deployed environment:

1. **The AppHost.** `WithCSharpRepl()` adds the hook only in run mode (`aspire run`) and only in the `Development` environment. `aspire publish` and `aspire deploy` get no hook variables.
2. **Release builds.** `StartupHookSupport=false` writes `"System.StartupHookProvider.IsSupported": false` to `FullstackTemplate.Server.runtimeconfig.json`. The runtime then ignores `DOTNET_STARTUP_HOOKS`. `FullstackTemplate.Server/Dockerfile` publishes with `-c Release`, so the deployed server cannot load the hook, even if the variable is set.
3. **The server.** `CSharpReplGuard` runs at startup. If the hook assembly is loaded and the environment is not `Development`, the server stops with this error: `The CSharpRepl startup hook is loaded in the 'Production' environment.`

The `Release` setting also turns off all other startup hooks, for example the hot reload agent. `Release` builds do not use hot reload.

## Connection security

The hook listens on a Unix domain socket in `$TMPDIR/csharprepl-connector/`. The directory has mode `0700`, so only the user that runs the server can connect. On Windows, the hook uses a named pipe for the current user. No network port opens.

## Use it

1. Install the tool.

   ```bash
   dotnet tool restore
   ```

2. Start the app.

   ```bash
   cd FullstackTemplate.AppHost
   aspire run
   ```

3. Get the process ID of `FullstackTemplate.Server`.

   ```bash
   dotnet csharprepl connect list
   ```

4. Start an interactive session. Use a real terminal, because the interactive prompt needs one.

   ```bash
   dotnet csharprepl connect <pid>
   ```

In the session, `Get<T>()` resolves a service from the root provider. To use the helpers that the skill uses (`Send`, `SendAs`, and `InScope`), load the prelude first:

```bash
NO_COLOR=1 dotnet csharprepl connect <pid> --eval-file .agents/skills/csharprepl/prelude.csx
```

## Update CSharpRepl

```bash
dotnet tool update csharprepl
```

Then restart the AppHost. The hook path contains the tool version, and the AppHost reads it at startup.

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| The AppHost log shows `CSharpRepl is disabled.` | The tool is not installed. | Run `dotnet tool restore`. Then restart the AppHost. |
| `connect list` shows no `FullstackTemplate.Server` row | The server is not running, or it started without the hook. | Make sure that the `server` resource is running. Then read the AppHost log. |
| `Connection refused` | The server process stopped or restarted. | Run `connect list` again and use the new process ID. |
| `Cannot resolve scoped service ... from root provider` | `Get<T>()` uses the root provider. | Load the prelude and use `InScope` or `Send`. |
| A patch stays active after the session | Patches live in the server process. | Run `#revert all`, or restart the server. |
