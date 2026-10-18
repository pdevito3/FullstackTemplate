---
name: csharprepl
description: Runs C# inside the live development server with CSharpRepl. Use to inspect runtime state (DI services, AppDbContext, configuration), send a MediatR command or query without HTTP, simulate a user or tenant, or wrap or replace a running method to reproduce a bug.
---

# CSharpRepl

CSharpRepl connects to the running `FullstackTemplate.Server` process and runs C# in it. The code runs against the live DI container, database, and configuration. The AppHost loads the CSharpRepl hook only in the `Development` environment. [docs/csharp-repl.md](../../../docs/csharp-repl.md) tells how the hook is wired and guarded.

## Steps

1. Start the app with `aspire run` (see `AGENTS.md`). If `dotnet csharprepl` is not found, run `dotnet tool restore`.
2. Get the process ID of the server. Use the `FullstackTemplate.Server` row. The `dotnet` row is the `dotnet run` wrapper.

   ```bash
   dotnet csharprepl connect list
   ```

3. Load the prelude once for each process ID. The prelude adds the usings and the `Send`, `SendAs`, and `InScope` helpers.

   ```bash
   NO_COLOR=1 dotnet csharprepl connect <pid> --eval-file .agents/skills/csharprepl/prelude.csx
   ```

4. Run your code. Pick the input mode from the table in [Input modes](#input-modes).
5. Before you finish, revert all patches. The step is done when `#patches` prints `No active patches.`

   ```bash
   NO_COLOR=1 dotnet csharprepl connect <pid> --streamPipedInput <<'EOF'
   #revert all
   #patches
   EOF
   ```

If the server restarts (a rebuild, a crash, or an AppHost restart), the process ID changes. All usings, variables, helpers, and patches are then gone. Start again at step 2.

## Input modes

Always set `NO_COLOR=1`. The interactive prompt needs a real terminal, so use one of these modes:

| Mode | Use for |
|------|---------|
| `--eval '<code>'` | One expression. The result prints, then the CLI exits. |
| `--streamPipedInput` with stdin | Many lines. Each line is one submission. `#` commands and C# can mix. |
| `--eval-file <file>.csx` | Multi-line C# with no `#` command. The whole file is one submission. |

```bash
NO_COLOR=1 dotnet csharprepl connect <pid> --eval 'await InScope<AppDbContext, int>(db => db.Tenants.CountAsync())'

NO_COLOR=1 dotnet csharprepl connect <pid> --streamPipedInput <<'EOF'
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
var tenants = await Send(new GetTenantList.Query(new TenantParametersDto()));
tenants.Select(t => t.Name).ToList()
EOF
```

Rules for submissions:

- A submission that ends with an expression and no `;` prints its value. A statement prints nothing.
- The first error stops the stream. The lines before it stay applied, and this includes patches.
- An exception prints only its message, for example `Please provide a tenant name.`
- `Console.WriteLine` writes to the server console log (the `server` resource in Aspire), not to the CLI.
- Usings, variables, and helpers stay in the server process across connections, until the process restarts. Add a `using` for each feature namespace that you use, for example `using FullstackTemplate.Server.Domain.Tenants.Features;`.

## Services and scopes

`Get<T>()` resolves from the root provider. It works for singletons, such as `IWebHostEnvironment`, `IConfiguration`, and `IHttpContextAccessor`. A scoped service, such as `AppDbContext` or `IMediator`, fails with `Cannot resolve scoped service ... from root provider`. Use the prelude helpers for scoped services:

| Helper | What it does |
|--------|--------------|
| `Send(request)` | Sends a MediatR request in a new scope, with no user. |
| `SendAs("<sub>", request)` | Sends a MediatR request as the user with this `sub` claim. |
| `InScope<TService, TResult>(service => ...)` | Runs code against one scoped service. |

`Send` goes through the full MediatR pipeline: `RequestTracingBehavior`, the handler, and the domain events. Commands write to the development database.

## Tenants and users

With no user, `AppDbContext.CurrentTenantId` is `null` and the tenant filter is off. `Send` then sees the rows of all tenants. To see the data as one tenant, use `SendAs` with the `Identifier` of a user in that tenant. If no user has this `Identifier`, `SendAs` stops with `No user has the identifier`.

```bash
NO_COLOR=1 dotnet csharprepl connect <pid> --streamPipedInput <<'EOF'
using FullstackTemplate.Server.Domain.Users.Features;
using FullstackTemplate.Server.Domain.Users.Dtos;
(await SendAs("<user identifier>", new GetUserList.Query(new UserParametersDto()))).Select(u => u.Username).ToList()
EOF
```

`IHttpContextAccessor` keeps the user in an `AsyncLocal`. A user that you set in one submission is gone in the next submission. Set the user and send the request in the same submission, as `SendAs` does.

## Patches

`#wrap` and `#replace` change a method for all callers in the process, HTTP requests included.

- `#wrap <Type>.<Method> with <lambda>` calls through to the original. The lambda gets `orig` first, then the instance, then the method parameters.
- `#replace <Type>.<Method> with <lambda>` swaps the body. The lambda gets the instance, then the method parameters.
- `#patches` lists the active patches with their IDs. `#revert <id>` removes one. `#revert all` removes all.
- Run `#patches` before you patch. A second patch on the same member gets a new ID, and the earlier patch stays in the list.
- A nested type needs `+` in the `#` command: `GetTenant+Handler`. In the lambda, use the C# name: `GetTenant.Handler`.
- The lambda compiles with the usings of the session. Add `using System.Threading;` for `CancellationToken` (the prelude does this).

```bash
NO_COLOR=1 dotnet csharprepl connect <pid> --streamPipedInput <<'EOF'
using FullstackTemplate.Server.Domain.Tenants.Features;
using FullstackTemplate.Server.Domain.Tenants.Dtos;
#patches
#wrap FullstackTemplate.Server.Domain.Tenants.Features.GetTenant+Handler.Handle with (Func<GetTenant.Handler, GetTenant.Query, CancellationToken, Task<TenantDto>> orig, GetTenant.Handler instance, GetTenant.Query request, CancellationToken ct) => { Console.WriteLine($"GetTenant {request.Id}"); return orig(instance, request, ct); }
#replace FullstackTemplate.Server.Domain.Tenants.Features.GetTenantList+Handler.Handle with (GetTenantList.Handler instance, GetTenantList.Query request, CancellationToken ct) => throw new TimeoutException("Simulated database timeout")
EOF
```

A patch lives only in memory. To keep a fix, change the source code, then remove the patch.
