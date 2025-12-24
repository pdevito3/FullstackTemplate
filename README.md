# Fullstack Aspire React Template

A production-ready .NET Aspire template featuring:
- **React 19** frontend with TanStack Router and React Query
- **Backend-For-Frontend (BFF)** pattern using Duende BFF
- **Configurable authentication** (Keycloak, FusionAuth, or Duende Demo)
- **OpenTelemetry** observability built-in
- **.NET 10** with Aspire 13.1

## Installation

```bash
dotnet new install FullstackTemplate.Aspire.React
```

## Usage

### Create with Keycloak (default)
```bash
dotnet new fullstack-aspire -n MyProject
```

### Create with FusionAuth
```bash
dotnet new fullstack-aspire -n MyProject --AuthProvider FusionAuth
```

### Create with Duende Demo
```bash
dotnet new fullstack-aspire -n MyProject --AuthProvider DuendeDemo
```

## Template Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--name` / `-n` | (required) | Project name |
| `--AuthProvider` | `Keycloak` | Auth provider: `Keycloak`, `FusionAuth`, or `DuendeDemo` |

## Project Structure

```
MyProject/
├── MyProject.AppHost/          # Aspire orchestration
├── MyProject.Server/           # Backend API
├── MyProject.Bff/              # Backend-For-Frontend
└── frontend/                   # React SPA
```

## Running the Application

```bash
cd MyProject.AppHost
dotnet run
```

This starts:
- Aspire Dashboard (with auth)
- Backend API server
- BFF proxy
- React development server
- Auth provider (Keycloak/FusionAuth in Docker, or external Duende Demo)

## Test Users

When using Keycloak or FusionAuth, test users are pre-configured:

| Email | Password | Roles |
|-------|----------|-------|
| admin@example.com | password123! | admin, user |
| user@example.com | password123! | user |

## Authentication Flow

1. User clicks "Login" in React app
2. BFF redirects to identity provider
3. User authenticates
4. BFF receives tokens and creates secure cookie
5. React app calls BFF endpoints with cookie
6. BFF forwards requests to backend API with access token

## License

MIT
