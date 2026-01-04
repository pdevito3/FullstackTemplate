# Fullstack Aspire React Template

A production-ready .NET Aspire template featuring:
- **React 19** frontend with TanStack Router and React Query
- **Backend-For-Frontend (BFF)** pattern using Duende BFF
- **Configurable authentication** (Keycloak, FusionAuth, or Duende Demo)
- **Multi-tenant architecture** with data isolation
- **OpenTelemetry** observability built-in
- **.NET 10** with Aspire 13.1

## Installation

```bash
dotnet new install FullstackTemplate
```

## Create a New Project

### With Duende Demo (default)
```bash
dotnet new fullstack-aspire -n MyProject
```

### With Keycloak
```bash
dotnet new fullstack-aspire -n MyProject --AuthProvider Keycloak
```

### With FusionAuth
```bash
dotnet new fullstack-aspire -n MyProject --AuthProvider FusionAuth
```

## Template Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--name` / `-n` | (required) | Project name |
| `--AuthProvider` | `DuendeDemo` | Auth provider: `DuendeDemo`, `Keycloak`, or `FusionAuth` |

## What You Get

```
MyProject/
├── MyProject.AppHost/     # Aspire orchestration (start here)
├── MyProject.Server/      # Backend API (.NET 10, CQRS, MediatR)
├── MyProject.Bff/         # BFF proxy (Duende BFF + YARP)
├── frontend/              # React SPA (React 19, TanStack, Tailwind)
├── tests/                 # Test projects
└── CLAUDE.md              # Development guide & patterns
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
- **40+ UI Components** - Based on shadcn/ui and Base UI
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
- **Auth Provider** - Keycloak/FusionAuth (Docker) or Duende Demo (external)

## Test Users

### Keycloak / FusionAuth
| Email | Password | Roles |
|-------|----------|-------|
| admin@example.com | password123! | admin, user |
| user@example.com | password123! | user |

### Duende Demo
| Username | Password |
|----------|----------|
| bob | bob |
| alice | alice |

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
2. **Read `CLAUDE.md`** for development patterns and guides
3. **Add your first entity** using the patterns in `.claude/rules/`
4. **Build your features** with the included CQRS patterns

## Development with Claude Code

This template includes comprehensive Claude Code integration:

- **`CLAUDE.md`** - Main development guide with architecture and patterns
- **`.claude/rules/`** - Detailed guides for backend patterns:
  - `controllers.md` - API controller patterns
  - `features-and-cqrs.md` - CQRS command/query patterns
  - `working-with-domain-entities.md` - Rich domain entity patterns
  - `dtos-and-mappings.md` - DTO and Mapperly patterns
  - `entity-configurations.md` - EF Core configuration patterns
  - `domain-events.md` - Domain event patterns
- **`.claude/skills/`** - Automated scaffolding skills

## License

MIT
