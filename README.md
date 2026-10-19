# Fullstack Aspire React Template

A production-ready .NET Aspire template featuring:
- **React 19** frontend with TanStack Router
- **OpenTelemetry** observability built-in
- **.NET 10** with Aspire 13.1

## Installation

```bash
dotnet new install FullstackTemplate
```

## Create a New Project

```bash
dotnet new fullstack-aspire -n MyProject
```

## Template Parameters

| Parameter | Default | Description |
|-----------|---------|-------------|
| `--name` / `-n` | (required) | Project name |
| `--no-restore` | `false` | Do not restore the NuGet packages after the project is created |

## Publish the Template

The package holds only the files that git tracks. Before you publish, run the smoke test. It packs the template, creates a project from the package, and fails if a local file that git ignores got into the package or the project.

```bash
./scripts/template-smoke-test.sh
```

## What You Get

```
MyProject/
├── MyProject.AppHost/     # Aspire orchestration (start here)
├── MyProject.Server/      # Backend API (.NET 10)
├── frontend/              # React SPA (React 19, TanStack, Tailwind)
├── tests/                 # Test projects
└── docs/                  # Development guides (telemetry)
```

### Backend Features
- **OpenTelemetry** - Distributed tracing and metrics

### Frontend Features
- **TanStack Router** - File-based routing with type safety

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
- **React Dev Server** - Vite with hot reload

### Run several stacks at the same time

To run a stack in each git worktree, start each one in isolated mode:

```bash
aspire run --isolated --apphost MyProject.AppHost
```

An isolated run gives each endpoint a random port and uses a copy of the user secrets. A normal run keeps port 5173 for the frontend.

## Next Steps

After creating your project:

1. **Run the app** to see everything working
2. **Read `docs/`** for development guides
3. **Build your features**

## License

MIT
