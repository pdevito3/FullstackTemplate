# AGENTS.md

Guidance for coding agents that work in this repository.

## Overview

This is a .NET Aspire application with a React frontend. It uses the Backend-For-Frontend (BFF) pattern. It has multi-tenancy, a CQRS architecture, and a configurable auth provider (Keycloak, FusionAuth, Authentik, or Duende Demo).

```
┌─────────────────────────────────────────────────────────────┐
│                    Aspire AppHost                           │
│                 (Orchestration Layer)                       │
└─────────────────────────────────────────────────────────────┘
         │                    │                    │
    ┌─────────┐          ┌─────────┐         ┌──────────┐
    │ Server  │◄────────►│   BFF   │◄───────►│ Frontend │
    │  (API)  │  (JWT)   │ (OIDC)  │ (REST)  │  (React) │
    └─────────┘          └─────────┘         └──────────┘
         │                    │
    ┌─────────┐        ┌──────────────┐
    │PostgreSQL│       │Auth Provider │
    └─────────┘        └──────────────┘
```

| Project | Purpose |
|---------|---------|
| `FullstackTemplate.AppHost` | Aspire orchestration. Defines all resources in `AppHost.cs` |
| `FullstackTemplate.Server` | Backend API (.NET 10, JWT auth, CQRS, multi-tenant) |
| `FullstackTemplate.Bff` | BFF proxy (Duende BFF and YARP, OIDC auth). Serves the SPA in production |
| `frontend` | React SPA (React 19, TanStack Router, TanStack Query, Tailwind v4) |
| `tests/` | Unit, integration, and functional tests |

## Docs

Read the doc for an area before you change code in that area.

| Doc | Content |
|-----|---------|
| [docs/backend.md](docs/backend.md) | Domain structure, new features, DI, API versioning, error responses, database, tests |
| [docs/telemetry.md](docs/telemetry.md) | Traces, metrics, logs, `Trace`, `AddEvent`, `Here()`, `TraceRoot`, `TraceContext`, named queries |
| [docs/multi-tenancy.md](docs/multi-tenancy.md) | Tenant isolation |
| [docs/authentication.md](docs/authentication.md) | BFF login flow, auth providers, test users |
| [docs/frontend.md](docs/frontend.md) | Frontend structure, API client, query hooks, routes, forms |
| [docs/deployment-northflank.md](docs/deployment-northflank.md) | Northflank deployment and auth provider setup |
| [docs/csharp-repl.md](docs/csharp-repl.md) | CSharpRepl in the running server: the hook wiring and the Development-only guards |

The detailed pattern rules are in `.claude/rules/backend/` and `.claude/rules/frontend/`. Obey them when you write code.

## Run the application

The Aspire CLI is a local tool in `dotnet-tools.json`. Run `dotnet tool restore` once, then use `dotnet aspire`.

```bash
dotnet aspire start --isolated --non-interactive --apphost FullstackTemplate.AppHost
```

This command starts the Aspire dashboard, the server, the BFF, the Vite dev server, and the auth provider in the background. `--isolated` gives the stack random ports and its own empty database, so that each worktree can run a stack at the same time. The `aspire-cli` skill gives the commands to wait for, inspect, restart, and stop the stack.

1. Before you make changes, start the stack and make sure that all resources are healthy.
2. If you change `AppHost.cs`, restart the stack. Other code changes hot-reload.
3. If a resource fails, read its logs and traces before you change code. The `aspire-telemetry` skill gives the commands.
4. To run C# in the live server (inspect DI or data, send a MediatR request, patch a method), use the `csharprepl` skill.

## Build and test

```bash
dotnet tool restore
dotnet build FullstackTemplate.slnx
dotnet test --solution FullstackTemplate.slnx

cd frontend
pnpm install
pnpm build
pnpm lint
```

## Rules

- Use pnpm for the frontend. Do not use npm.
- Put all NuGet versions in `Directory.Packages.props`.
- Use `docker compose` with a space. Do not use `docker-compose`.
- Do not install the Aspire workload. It is obsolete.
- Do not use persistent containers early in development. They keep state between restarts.
- Do not edit generated files, such as `frontend/src/routeTree.gen.ts` and the EF migration snapshots.

## Aspire

- Before you add a resource, use the `list integrations` tool. Use the integration version that matches `Aspire.AppHost.Sdk`.
- Get the docs for an integration with the `get integration docs` tool, then obey them.
- To update Aspire, run `aspire update`. Then update the other packages for compatibility.
- Prefer the official docs: https://aspire.dev, https://learn.microsoft.com/dotnet/aspire, and https://nuget.org.

## MCP tools

- **Aspire MCP**: resources, console logs, structured logs, traces, and integrations.
- **Playwright MCP**: browser tests. Get the endpoint URLs from the `list resources` tool.
