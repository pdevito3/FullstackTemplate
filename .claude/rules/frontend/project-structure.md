---
description: Project directory structure and important files guide
globs: src/**/*
alwaysApply: false
---

# Project Directory Structure

This rule outlines the directory structure and important files in the PeakLIMS SPA project.

## Root Directory Structure

```
peaklims-spa/
├── src/                     # Source code directory
├── public/                  # Static assets served directly
├── rules/                   # Project rules and guidelines
├── e2e/                     # End-to-end Playwright tests
├── tests/                   # Test files and utilities
├── node_modules/            # Dependencies (git-ignored)
├── playwright-report/       # Test reports (git-ignored)
├── test-results/            # Test results (git-ignored)
├── package.json             # Package configuration
├── pnpm-lock.yaml          # Lock file for pnpm
├── tsconfig.json           # TypeScript configuration
├── vite.config.ts          # Vite build configuration
├── tailwind.config.js      # Tailwind CSS configuration
├── components.json         # UI components configuration
├── index.html              # Entry HTML file
└── CLAUDE.md               # AI assistant guidelines

```

## Source Code Organization (`/src`)

### 1. **Domain Directory** (`/src/domain/`)
Organized by business entities following Domain-Driven Design principles:

```
domain/
├── accessions/             # Accession management
├── accession-attachments/  # File attachments for accessions
├── accession-comments/     # Comments and notes
├── accession-contacts/     # Contact associations
├── contacts/               # Contact management
├── containers/             # Sample containers
├── organizations/          # Organization management
├── organization-contacts/  # Organization contact relationships
├── patients/               # Patient management
├── peak-organizations/     # Peak-specific organization features
├── quick-actions/          # Quick action workflows
├── samples/                # Sample management
└── test-orders/            # Test order management
```

Each domain folder follows this standard structure:
```
[domain-name]/
├── apis/                   # API calls and React Query hooks
│   ├── *.keys.ts          # React Query key factories
│   └── *.tsx              # API hooks (e.g., get-*, add-*, update-*)
├── components/            # Domain-specific UI components
├── features/              # Complex feature components
├── types/                 # TypeScript type definitions
│   └── index.ts          # Exported types
└── index.ts              # Public exports
```

### 2. **Routes Directory** (`/src/routes/`)
File-based routing with TanStack Router:

```
routes/
├── __root.tsx             # Root layout component
├── _auth-layout.tsx       # Authenticated routes layout
├── _auth-layout/          # Protected route pages
│   ├── accessions/        # Accession pages
│   │   ├── $accessionId.tsx  # Dynamic accession detail
│   │   └── index.tsx         # Accessions list
│   ├── queue/             # Work queue
│   ├── receiving/         # Sample receiving
│   ├── reporting/         # Test reporting
│   ├── runs/              # Test runs
│   └── settings*.tsx      # Settings pages
└── index.tsx              # Home/landing page
```

### 3. **Components Directory** (`/src/components/`)
Shared UI components used across domains:

```
components/
├── ui/                    # Base UI components (Radix UI based)
│   ├── button.tsx
│   ├── form.tsx
│   ├── input.tsx
│   ├── dialog.tsx
│   └── ...
├── data-table/           # Reusable data table components
├── file-upload/          # File upload components
├── notifications/        # Toast notifications
├── svgs/                 # SVG icon components
└── [component].tsx       # Other shared components
```

### 4. **Services Directory** (`/src/services/`)
Core application services:

```
services/
├── api-client.tsx        # Axios instances and interceptors
└── auth.tsx              # Authentication service
```

### 5. **Other Important Directories**

```
src/
├── assets/               # Static assets (logos, images)
├── hooks/                # Custom React hooks
├── lib/                  # Utility libraries
├── types/                # Global TypeScript types
└── utils/                # Utility functions
```

## Configuration Files

### Core Configuration
- `package.json` - Dependencies and scripts
- `tsconfig.json` - TypeScript compiler options
- `vite.config.ts` - Build tool configuration
- `tailwind.config.js` - Tailwind CSS customization
- `components.json` - shadcn/ui components configuration

### Development Configuration
- `playwright.config.ts` - E2E test configuration
- `postcss.config.js` - PostCSS configuration

### Documentation
- `CLAUDE.md` - AI assistant project-specific instructions
- `README.md` - Project documentation
- `CONTRIBUTING.md` - Contribution guidelines

## Import Path Aliases

The project uses path aliases configured in `vite.config.ts`:
- `@/*` → `./src/*`

Example usage:
```typescript
import { Button } from "@/components/ui/button"
import { useAuthUser } from "@/services/auth"
import { Patient } from "@/domain/patients/types"
```

## Key Architectural Patterns

1. **Domain-Driven Organization**: Features are organized by business domain rather than technical layers
2. **File-Based Routing**: Routes are automatically generated from the file structure
3. **Standardized API Hooks**: All API calls follow consistent naming patterns (get*, add*, update*, delete*)
4. **Type Safety**: Strict TypeScript configuration with comprehensive type definitions
5. **Component Library**: Mix of custom components and Radix UI primitives styled with Tailwind

## Important Files to Know

- `/src/main.tsx` - Application entry point
- `/src/routeTree.gen.ts` - Auto-generated route tree (DO NOT EDIT)
- `/src/services/api-client.tsx` - API configuration and interceptors
- `/src/services/auth.tsx` - Authentication hooks and utilities
- `/src/lib/site-config.ts` - Site-wide configuration
- `/src/components/ui/*` - Base UI component library