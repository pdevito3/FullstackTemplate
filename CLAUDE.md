# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Production-ready .NET Aspire template with React frontend using the Backend-For-Frontend (BFF) pattern. Supports three authentication providers: Keycloak, FusionAuth, or Duende Demo.

## Running the Application

```bash
# Start everything (recommended)
cd FullstackTemplate.AppHost
aspire run

# Or use dotnet run
dotnet run --project FullstackTemplate.AppHost
```

This starts: Aspire Dashboard, Backend API, BFF proxy, React dev server, and auth provider.

## Build Commands

```bash
# Backend
dotnet build

# Frontend
cd frontend
pnpm install
pnpm dev      # dev server
pnpm build    # production build
pnpm lint     # ESLint
```

## Architecture

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
                              │
                       ┌──────────────┐
                       │Auth Provider │
                       └──────────────┘
```

### Projects

- **FullstackTemplate.AppHost** - Aspire orchestration, defines all resources
- **FullstackTemplate.Server** - Backend API (.NET 10, JWT auth, API versioning)
- **FullstackTemplate.Bff** - BFF proxy (Duende BFF + YARP, OIDC auth)
- **frontend** - React SPA (React 19, TanStack Router, React Query, Tailwind)

### Authentication Flow

1. User clicks Login in React app
2. Frontend navigates to `/bff/login`
3. BFF redirects to OIDC provider
4. Provider redirects back to `/signin-oidc`
5. BFF creates secure `__Host-bff` cookie
6. React app calls `/bff/user` to get claims
7. API calls go through BFF at `/api/v1/*`

### Backend Patterns

- **Dependency Injection**: Scrutor with marker interfaces (`ISingletonService`, `ITransientService`)
- **Domain Design**: `BaseEntity` (audit fields, soft delete), `ValueObject` (immutable types)
- **API Versioning**: `Asp.Versioning.Http`, endpoints use `MapToApiVersion()`
- **Observability**: OpenTelemetry + Serilog throughout
- **Error Handling**: RFC 7807 Problem Details

## Service Registration

Automatic DI registration using marker interfaces:

```csharp
// Default: Scoped lifetime (no marker needed)
public interface IMyService { }
public class MyService : IMyService { }

// Singleton lifetime
public interface IMySingletonService { }
public class MySingletonService : IMySingletonService, ISingletonService { }

// Transient lifetime
public interface IMyTransientService { }
public class MyTransientService : IMyTransientService, ITransientService { }
```

Services are auto-registered by calling `builder.Services.AddApplicationServices()` in Program.cs.

## API Versioning

Location: `Server/Resources/Extensions/ApiVersioningExtension.cs`

URL segment versioning at `/api/v{version}/...`:

```csharp
// Setup in Program.cs
builder.Services.AddApiVersioningExtension();
var apiVersionSet = app.GetApiVersionSet();

// Map endpoints to version
var api = app.MapGroup("api/v{version:apiVersion}")
    .WithApiVersionSet(apiVersionSet);

api.MapGet("/weather", () => { ... })
    .MapToApiVersion(new ApiVersion(1, 0));

// Or use controller attributes
[Route("api/v{v:apiVersion}/weather")]
[ApiVersion("1.0")]
```

To add a new version, update `GetApiVersionSet()` with `.HasApiVersion(new ApiVersion(2, 0))`.

## Telemetry

Location: `Server/Resources/Telemetry.cs`

Centralized OpenTelemetry instrumentation with pre-configured metrics:

```csharp
// Start a trace span
using var activity = Telemetry.Source.StartActivity("operation-name");
activity?.SetTag("key", "value");

// Record metrics
Telemetry.Requests.Add(1, new("endpoint", "/api/weather"));
Telemetry.RequestDuration.Record(elapsed.TotalMilliseconds, new("endpoint", "/api/weather"));

// Record errors
Telemetry.RecordError(activity, exception);
```

Built-in metrics:
- `app.requests` - Counter for requests processed
- `app.errors` - Counter for errors encountered
- `app.request.duration` - Histogram for request duration (ms)

View traces and metrics in the Aspire Dashboard at http://localhost:18888.

## Problem Details

Standard RFC 7807 error responses enabled via `builder.Services.AddProblemDetails()`.

Unhandled exceptions automatically return structured JSON:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.6.1",
  "title": "An error occurred",
  "status": 500,
  "detail": "..."
}
```

The global exception handler is enabled with `app.UseExceptionHandler()`.

### Frontend Patterns

- **API Client**: `/frontend/src/api/client.ts` (Axios with BFF integration)
- TODO other

## Working with Aspire

From AGENTS.md:

1. Always run `aspire run` before making changes to verify starting state
2. Changes to `AppHost.cs` require restart; other code changes hot-reload
3. Use Aspire MCP tools to check resource status and debug issues
4. Use `list integrations` tool before adding resources; get docs for correct versions
5. For debugging, check structured logs, console logs, and traces via MCP tools
6. The Aspire workload is obsolete - never attempt to install it
7. Avoid persistent containers early in development to prevent state issues

## Test Users For Auth 

| Email | Password | Roles |
|-------|----------|-------|
| admin@example.com | password123! | admin, user |
| user@example.com | password123! | user |

Duende demo uses `bob/bob` or `alice/alice` for username/password

## Key Configuration

- **Centralized Package Management**: All NuGet versions in `Directory.Packages.props`
- **Frontend Package Manager**: pnpm (not npm)
- **Docker**: Use `docker compose` with a space (not `docker-compose`)

## MCP Tools Available

- **Aspire MCP**: Resource management, logs, traces, integrations
- **Playwright MCP**: Browser automation for functional testing
