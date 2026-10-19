---
name: aspire-cli
description: Starts, stops, and drives the full stack with the Aspire CLI. Use to run the app (in a worktree, always isolated), to find the URLs of a running stack, to wait for a resource, to read console logs, or to restart or rebuild a resource. For OpenTelemetry logs, spans, and traces, use aspire-telemetry.
---

# Aspire CLI

The repository pins the Aspire CLI as a local .NET tool in `dotnet-tools.json`. Run it as `dotnet aspire`. If the command is not found, run `dotnet tool restore`. A global `aspire` of the same version works the same.

The Aspire MCP server in `.mcp.json` runs the same pinned CLI (`dotnet aspire agent mcp`). Its tools (`list_resources`, `list_console_logs`, `execute_resource_command`, `list_apphosts`, `select_apphost`, and more) give the same data as the commands below.

## Isolation

An isolated run (`--isolated`) gives the stack its own copy of everything that can conflict:

| Item | Normal run | Isolated run |
|---|---|---|
| Ports of the dashboard, the server, the BFF, Vite, and the auth containers | Fixed (Vite is `5173`) | Random |
| User secrets | The AppHost secrets | A copy, deleted at the end of the run |
| Postgres data (app and auth provider) | A volume for each checkout path | No volume. The data is empty at each start. |

Thus a worktree run never shares a port or a database with another run. The Docker images, the NuGet and pnpm caches, and the Duende demo identity server stay shared.

An isolated run starts with an empty database. The server applies the migrations at startup. Make sure that the test data that you need is created by your steps, not by an earlier run. Aspire deletes the database volume when the stack stops.

For a Playwright run against an isolated stack, set `BASE_URL` to the `webfrontend` URL and `SERVER_URL` to the `server` https URL.

## Start

1. Select the run mode. In a git worktree, always use `--isolated`. In the main checkout, use `--isolated` when another stack runs at the same time.
2. Start the stack in the background from the checkout root:

   ```bash
   dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
   ```

3. Wait for the server and the BFF. The BFF can be healthy before the server is.

   ```bash
   dotnet aspire wait server --apphost FullstackTemplate.AppHost
   dotnet aspire wait bff --apphost FullstackTemplate.AppHost
   ```

4. Get the URLs. The app URL is the `webfrontend` URL. The dashboard URL is in the `dotnet aspire ps` output.

   ```bash
   dotnet aspire describe --apphost FullstackTemplate.AppHost
   ```

The step is done when `dotnet aspire describe` shows each resource as running and healthy.

## Select the AppHost

Each command (except `ps`) selects the AppHost under the current directory. The worktrees are in `.claude/worktrees/`, under the main checkout. Thus, from the main checkout, a worktree stack is also under the current directory. With two candidates, a non-interactive command stops with an error.

Always add `--apphost FullstackTemplate.AppHost` and run the command from the root of your checkout. The path then names exactly one stack.

## Commands

All rows need `--apphost FullstackTemplate.AppHost` (see [Select the AppHost](#select-the-apphost)).

| You need | Command |
|---|---|
| Running stacks and their dashboard URLs | `dotnet aspire ps` |
| State, health, and URLs of each resource | `dotnet aspire describe` (add `--format Json` for a script) |
| Wait for a resource | `dotnet aspire wait <resource> --status healthy` |
| Console output (stdout) of a resource | `dotnet aspire logs <resource> -n 100` |
| Search the console output | `dotnet aspire logs <resource> --search "<text>"` |
| Commands of a resource | `dotnet aspire resource <resource> --help` |
| Restart a resource | `dotnet aspire resource <resource> restart` |
| Rebuild a .NET project and restart it | `dotnet aspire resource <resource> rebuild` |
| Stop the stack | `dotnet aspire stop` |

The resource names are the names in `FullstackTemplate.AppHost/AppHost.cs`: `server`, `bff`, `webfrontend`, `postgres`, `appdb`, and `tests` (the Labcoat test UI).

## Code changes

| You changed | Do |
|---|---|
| Frontend code | Nothing. Vite reloads the page. |
| Server or BFF code | `dotnet aspire resource server rebuild` (or `bff`) |
| `AppHost.cs` or another AppHost file | Stop the stack, then start it again |

## Stop

Stop your stack before you finish. The step is done when `dotnet aspire ps` does not show your AppHost path.

```bash
dotnet aspire stop --apphost FullstackTemplate.AppHost
```

Stop only the stack of your checkout. Other stacks belong to other worktrees or to the user.
