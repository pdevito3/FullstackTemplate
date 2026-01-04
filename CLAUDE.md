# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

Production-ready .NET Aspire application with React frontend using the Backend-For-Frontend (BFF) pattern. Features multi-tenancy, CQRS architecture, and configurable authentication (Keycloak, FusionAuth, Authentik, or Duende Demo).

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
         │                    │
    ┌─────────┐        ┌──────────────┐
    │PostgreSQL│       │Auth Provider │
    └─────────┘        └──────────────┘
```

### Projects

- **FullstackTemplate.AppHost** - Aspire orchestration, defines all resources
- **FullstackTemplate.Server** - Backend API (.NET 10, JWT auth, CQRS, multi-tenant)
- **FullstackTemplate.Bff** - BFF proxy (Duende BFF + YARP, OIDC auth)
- **frontend** - React SPA (React 19, TanStack Router, React Query, Tailwind v4)

### Authentication Flow

1. User clicks Login in React app
2. Frontend navigates to `/bff/login`
3. BFF redirects to OIDC provider
4. Provider redirects back to `/signin-oidc`
5. BFF creates secure `__Host-bff` cookie
6. React app calls `/bff/user` to get claims
7. API calls go through BFF at `/api/v1/*`

## Backend Patterns

### Domain Structure

Each domain entity follows this folder structure:

```
Server/Domain/
└── [EntityName]s/
    ├── [EntityName].cs              # Domain entity
    ├── Controllers/v1/              # API controllers
    ├── Dtos/                        # DTOs (read/write)
    ├── Features/                    # CQRS commands/queries
    ├── Mappings/                    # Mapperly mappers
    ├── Models/                      # Internal models
    └── DomainEvents/                # Domain events
```

### Key Backend Concepts

- **Rich Domain Entities**: Private setters, factory methods, encapsulated business logic
- **CQRS with MediatR**: Commands for writes, Queries for reads
- **Vertical Slice Architecture**: Each feature is self-contained
- **Mapperly**: Compile-time source generation for DTO mapping
- **QueryKit**: Filtering and sorting for list endpoints
- **Value Objects**: `EmailAddress`, `UserRole`, etc. with SmartEnum
- **Domain Events**: Queued in entities, dispatched on SaveChanges

### Service Registration

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

## Multi-Tenancy

The application supports multi-tenant data isolation:

### How It Works

1. **ITenantable Interface**: Entities implement `ITenantable` to include `TenantId`
2. **Query Filters**: `AppDbContext` applies automatic tenant filtering
3. **TenantIdProvider**: Resolves current tenant from user claims (cached with FusionCache)
4. **Automatic Assignment**: New entities get TenantId set automatically

### Making an Entity Multi-Tenant

```csharp
public class MyEntity : BaseEntity, ITenantable
{
    public Guid TenantId { get; set; }
    // ... other properties
}
```

### Tenant Resolution

The `ITenantIdProvider` service:
- Looks up tenant by user identifier
- Caches results for 30 minutes with FusionCache
- Provides fail-safe caching for 2 hours

## API Versioning

Location: `Server/Resources/Extensions/ApiVersioningExtension.cs`

URL segment versioning at `/api/v{version}/...`:

```csharp
// Setup in Program.cs
builder.Services.AddApiVersioningExtension();
var apiVersionSet = app.GetApiVersionSet();

// Controller-based (recommended)
[Route("api/v{v:apiVersion}/[controller]")]
[ApiVersion("1.0")]
public sealed class CustomersController : ControllerBase { }

// Minimal API endpoints
api.MapGet("/weather", () => { ... })
    .MapToApiVersion(new ApiVersion(1, 0));
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

## Frontend Patterns

### Project Structure

```
frontend/src/
├── api/                    # API client and React Query hooks
│   ├── client.ts          # Axios instance with BFF config
│   └── hooks.ts           # useAuth, useWeatherForecast, etc.
├── components/
│   ├── ui/                # 40+ reusable UI components
│   ├── filter-builder/    # Advanced filter component
│   ├── app-sidebar.tsx    # Main navigation sidebar
│   ├── theme-provider.tsx # Theme context
│   └── theme-toggle.tsx   # Light/dark/system toggle
├── routes/                 # TanStack Router file-based routes
├── hooks/                  # Custom React hooks
├── lib/                    # Utilities (cn, etc.)
└── main.tsx               # App entry point
```

### API Client

Location: `frontend/src/api/client.ts`

Axios configured for BFF integration:

```typescript
import { apiClient } from '@/api/client'

// All requests include credentials and CSRF header
const response = await apiClient.get('/api/v1/users')

// Or use the typed API functions
import { weatherApi, authApi } from '@/api/client'

const forecasts = await weatherApi.getForecasts()
const user = await authApi.getUser()
```

Key configuration:
- `withCredentials: true` - Sends cookies cross-origin
- `X-CSRF: 1` header - CSRF protection for BFF

### React Query Hooks

Location: `frontend/src/api/hooks.ts`

```typescript
// Fetch weather data
const { data, isLoading, error } = useWeatherForecast()

// Auth state (parsed from BFF claims)
const { isLoggedIn, username, logoutUrl } = useAuth()

// Auth actions
const { login, logout } = useAuthActions()
```

### Routing (TanStack Router)

File-based routing in `frontend/src/routes/`:

```typescript
// routes/users.tsx
import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/users')({
  component: UsersPage,
})

function UsersPage() {
  return <div>Users</div>
}
```

Navigation:
```typescript
import { Link, useNavigate } from '@tanstack/react-router'

<Link to="/users">Users</Link>

const navigate = useNavigate()
navigate({ to: '/users/$id', params: { id: '123' } })
```

### Theme System

Location: `frontend/src/components/theme-provider.tsx`

```typescript
import { useTheme } from '@/components/theme-provider'

function MyComponent() {
  const { theme, setTheme } = useTheme()
  // theme: 'light' | 'dark' | 'system'
}
```

Theme persists to localStorage and respects system preference.

### UI Components

Location: `frontend/src/components/ui/`

40+ components based on shadcn/ui and Base UI:

| Category | Components |
|----------|------------|
| **Forms** | Input, Select, Combobox, Autocomplete, MultiSelect, Checkbox, DatePicker |
| **Layout** | Card, Dialog, Sheet, Drawer, Popover, Tooltip, Collapsible |
| **Data** | Table, DataTable, Avatar, Badge |
| **Navigation** | Breadcrumb, Sidebar, DropdownMenu |

Usage:
```typescript
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Card, CardHeader, CardContent } from '@/components/ui/card'
```

### Filter Builder

Location: `frontend/src/components/filter-builder/`

Advanced filter component that generates QueryKit-compatible filter strings:

```typescript
import { FilterBuilder } from '@/components/filter-builder/filter-builder'
import { toQueryKitString } from '@/components/filter-builder/utils/querykit-converter'

const filterConfig = [
  { propertyKey: 'firstName', propertyLabel: 'First Name', controlType: 'text' },
  { propertyKey: 'status', propertyLabel: 'Status', controlType: 'multiselect',
    options: [{ value: 'active', label: 'Active' }] },
  { propertyKey: 'createdAt', propertyLabel: 'Created', controlType: 'date' },
]

<FilterBuilder
  filterOptions={filterConfig}
  onChange={(state) => {
    const queryString = toQueryKitString(state)
    // queryString: "firstName == John && status ^^ [active]"
  }}
/>
```

Supports: text, number, date, boolean, multiselect with AND/OR grouping.

### Forms (React Hook Form + Zod)

```typescript
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'

const schema = z.object({
  name: z.string().min(1, 'Required'),
  email: z.string().email(),
})

function MyForm() {
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: { name: '', email: '' },
  })

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <Input {...form.register('name')} />
      <Input {...form.register('email')} />
      <Button type="submit">Submit</Button>
    </form>
  )
}
```

## Database Patterns

### AppDbContext Features

Location: `Server/Databases/AppDbContext.cs`

- **Soft Delete**: Entities with `IsDeleted` are filtered automatically
- **Audit Fields**: `CreatedOn`, `CreatedBy`, `LastModifiedOn`, `LastModifiedBy` auto-populated
- **Tenant Filtering**: Multi-tenant queries filtered by TenantId
- **Domain Events**: Dispatched after SaveChanges via MediatR

### Entity Configuration

Location: `Server/Databases/EntityConfigurations/`

```csharp
public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.Property(e => e.Name).HasMaxLength(200).IsRequired();

        // Value object configuration
        builder.OwnsOne(e => e.Email, email =>
        {
            email.Property(e => e.Value).HasColumnName("email").HasMaxLength(320);
        });
    }
}
```

### Migrations

```bash
# Add a migration
dotnet ef migrations add MyMigration --project FullstackTemplate.Server

# Apply migrations (auto-runs on startup in development)
dotnet ef database update --project FullstackTemplate.Server
```

## Working with Aspire

From AGENTS.md:

1. Always run `aspire run` before making changes to verify starting state
2. Changes to `AppHost.cs` require restart; other code changes hot-reload
3. Use Aspire MCP tools to check resource status and debug issues
4. Use `list integrations` tool before adding resources; get docs for correct versions
5. For debugging, check structured logs, console logs, and traces via MCP tools
6. The Aspire workload is obsolete - never attempt to install it
7. Avoid persistent containers early in development to prevent state issues

## Test Users

### Keycloak / FusionAuth / Authentik

| Email | Password | Roles |
|-------|----------|-------|
| admin@example.com | password123! | admin, user |
| user@example.com | password123! | user |

### Authentik Admin

| Email | Password |
|-------|----------|
| akadmin@example.com | password123! |

### Duende Demo

| Username | Password |
|----------|----------|
| bob | bob |
| alice | alice |

## Key Configuration

- **Centralized Package Management**: All NuGet versions in `Directory.Packages.props`
- **Frontend Package Manager**: pnpm (not npm)
- **Docker**: Use `docker compose` with a space (not `docker-compose`)

## Northflank Deployment

The `northflank.json` file provides Infrastructure-as-Code for deploying to Northflank.

### Keycloak Post-Deployment Setup

When using Keycloak as the auth provider (`--AuthProvider Keycloak`), the Northflank template deploys:
- PostgreSQL for Keycloak
- Keycloak Server (public)

**After first deployment:**

1. Get the Keycloak server URL from Northflank dashboard (e.g., `https://keycloak--myproject.us-central.northflank.app`)

2. Update `KEYCLOAK_BASE_URL` argument in Northflank with this URL

3. Access Keycloak admin at `{KEYCLOAK_BASE_URL}/admin/`

4. Login with admin credentials (`admin` / value of `KEYCLOAK_ADMIN_PASSWORD`)

5. Create a realm named `aspire`

6. Create a Client:
   - Client ID: `aspire-app`
   - Client authentication: ON
   - Valid redirect URIs: `https://{your-bff-url}/signin-oidc`
   - Copy the client secret and update `AUTH_CLIENT_SECRET` argument

7. Create roles (`admin`, `user`) and users as needed

8. Configure role mappings for users

### Authentik Post-Deployment Setup

When using Authentik as the auth provider (`--AuthProvider Authentik`), the Northflank template deploys:
- PostgreSQL for Authentik
- Redis for Authentik
- Authentik Server (public)
- Authentik Worker

**After first deployment:**

1. Get the Authentik server URL from Northflank dashboard (e.g., `https://authentik-server--myproject.us-central.northflank.app`)

2. Update `AUTHENTIK_BASE_URL` argument in Northflank with this URL

3. Access Authentik admin at `{AUTHENTIK_BASE_URL}/if/admin/`

4. Login with bootstrap credentials (default: `akadmin` / value of `AUTHENTIK_BOOTSTRAP_PASSWORD`)

5. Configure OAuth2 Provider:
   - Go to Applications → Providers → Create
   - Type: OAuth2/OpenID Provider
   - Name: FullstackTemplate Provider
   - Authorization flow: default-provider-authorization-implicit-consent
   - Client ID: `aspire-app`
   - Client Secret: (match `AUTH_CLIENT_SECRET` argument)
   - Redirect URIs: `https://{your-bff-url}/signin-oidc`
   - Scopes: openid, email, profile, offline_access

6. Create Application:
   - Go to Applications → Applications → Create
   - Name: FullstackTemplate
   - Slug: `aspire-app`
   - Provider: Select the provider created above

7. Create groups (`admin`, `user`) and users as needed

## MCP Tools Available

- **Aspire MCP**: Resource management, logs, traces, integrations
- **Playwright MCP**: Browser automation for functional testing

## Adding a New Feature

For detailed patterns, see `.claude/rules/backend/`:

1. **Create Domain Entity** (`working-with-domain-entities.md`)
   - Rich entity with private setters, factory methods
   - Value objects for complex properties

2. **Add Entity Configuration** (`entity-configurations.md`)
   - Configure in `Databases/EntityConfigurations/`
   - Run migration

3. **Create DTOs and Mappings** (`dtos-and-mappings.md`)
   - Read DTOs, Creation DTOs, Update DTOs
   - Mapperly mapper

4. **Implement Features** (`features-and-cqrs.md`)
   - Add/Get/Update/Delete commands and queries
   - FluentValidation for request validation

5. **Add Controller** (`controllers.md`)
   - Thin controller delegating to MediatR
   - Proper route attributes and versioning

6. **Add Domain Events** (`domain-events.md`)
   - Queue events in entity methods
   - Create handlers for side effects
