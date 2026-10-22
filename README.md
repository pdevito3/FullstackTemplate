# Fullstack Aspire React Template

A production-ready .NET Aspire template featuring:
- **React 19** frontend with TanStack Router and React Query
- **Backend-For-Frontend (BFF)** pattern using Duende BFF
- **Configurable authentication** - one setting selects the auth provider, and a new provider is one folder in the AppHost
- **Multi-tenant architecture** with data isolation
- **OpenTelemetry** observability built-in
- **.NET 10** with Aspire 13.1

<!-- init:start - scripts/init.cs deletes this section when it renames the app -->
## Create a New App

1. Clone the repo, or use the GitHub "Use this template" button.
2. In the root of the clone, run the init script:

   ```bash
   dotnet run scripts/init.cs --name MyProject --auth-provider Keycloak
   ```

3. Commit the result.

The script works on all platforms. It changes only a clean git tree. A second run with the same arguments changes nothing.

All auth providers stay in the app, and the `AuthProvider` setting selects one at runtime. To delete a provider that you do not use, see [docs/authentication.md](docs/authentication.md#delete-a-provider).

| Option | Description |
|--------|-------------|
| `--name` | Renames `FullstackTemplate` to this name in file names, folder names, and file contents. Use a C# namespace, for example `Acme.Shop`. |
| `--auth-provider` | Sets the default `AuthProvider` of the AppHost. The value is the name of a folder in `FullstackTemplate.AppHost/AuthProviders/`. |

See [ADR 0007](docs/adr/0007-regular-repo-instead-of-template.md) for the reasons.
<!-- init:end -->

## What You Get

```
MyProject/
├── MyProject.AppHost/     # Aspire orchestration (start here)
├── MyProject.Server/      # Backend API (.NET 10, CQRS, MediatR)
├── MyProject.Bff/         # BFF proxy (Duende BFF + YARP)
├── frontend/              # React SPA (React 19, TanStack, Tailwind)
├── tests/                 # Test projects
├── docs/                  # Development guides (backend, telemetry, auth, ...)
└── AGENTS.md              # Agent guide & index of docs
```

### Backend Features
- **CQRS with MediatR** - Vertical slice architecture
- **Rich Domain Entities** - DDD patterns with value objects
- **Multi-tenancy** - Automatic tenant isolation
- **Soft Delete** - Audit fields and soft delete built-in
- **API Versioning** - URL segment versioning (`/api/v1/...`)
- **OpenTelemetry** - Distributed tracing and metrics
- **PostgreSQL** - With EF Core and migrations

### Frontend Features
- **TanStack Router** - File-based routing with type safety
- **TanStack Query** - Server state management
- **TanStack Table** - Data tables with sorting/filtering
- **30+ UI Components** - Based on shadcn/ui and React Aria Components, animated with Animate UI
- **Filter Builder** - Advanced filtering with QueryKit integration
- **Theme System** - Light/dark/system mode
- **React Hook Form + Zod** - Form handling with validation

## Running the Application

```bash
cd MyProject.AppHost
dotnet run
```

Or use the Aspire CLI:
```bash
cd MyProject.AppHost
aspire run
```

This starts:
- **Aspire Dashboard** - View logs, traces, and metrics
- **Backend API** - .NET API server
- **BFF Proxy** - Handles auth and proxies to API
- **React Dev Server** - Vite with hot reload
- **Auth Provider** - the provider that the `AuthProvider` setting selects

### Run several stacks at the same time

To run a stack in each git worktree, start each one in isolated mode:

```bash
dotnet tool restore   # once: installs the pinned Aspire CLI as `dotnet aspire`
dotnet aspire run --isolated --apphost MyProject.AppHost
```

An isolated run gives each endpoint a random port and uses a copy of the user secrets. It also starts Postgres with no data volume, so each run gets an empty database. A normal run keeps port 5173 for the frontend and keeps a separate data volume for each checkout path.

## Auth Provider

To select the auth provider, add a provider, or find the test users, see [docs/authentication.md](docs/authentication.md).

## Authentication Flow

```
┌──────────┐     ┌─────────┐     ┌───────────────────┐
│  React   │────►│   BFF   │────►│   Auth Provider   │
│   App    │◄────│ (OIDC)  │◄────│  (e.g. Keycloak)  │
└──────────┘     └─────────┘     └───────────────────┘
     │                │
     │   Cookie       │  JWT Token
     │                ▼
     │          ┌─────────┐
     └─────────►│  API    │
       via BFF  │ Server  │
                └─────────┘
```

1. User clicks Login → navigates to `/bff/login`
2. BFF redirects to identity provider
3. User authenticates with provider
4. Provider redirects back to `/signin-oidc`
5. BFF creates secure `__Host-bff` cookie
6. React app calls `/bff/user` to get claims
7. API calls go through BFF at `/api/v1/*`

## Next Steps

After creating your project:

1. **Run the app** to see everything working
2. **Read `AGENTS.md` and `docs/`** for development patterns and guides
3. **Add your first entity** using the patterns in `docs/backend/`
4. **Build your features** with the included CQRS patterns

## Development with Claude Code

This template includes comprehensive Claude Code integration:

- **`AGENTS.md`** - Main agent guide with the architecture and an index of `docs/`
- **`docs/`** - Guides for the backend, telemetry, multi-tenancy, auth, and frontend
- **`docs/backend/`** - Backend pattern rules: entities, features, DTOs, endpoints (HTTP, gRPC, MCP), domain events, and EF configurations
- **`docs/frontend/`** - Frontend pattern rules: server state, client state, and code style
- **`.claude/skills/`** - Automated scaffolding skills

## License

MIT
