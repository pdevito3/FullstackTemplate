# Frontend

The frontend is a React 19 SPA in `frontend/`. It uses TanStack Router, TanStack Query, Tailwind v4, and pnpm. The detailed rules are in `.claude/rules/frontend/`.

## Commands

```bash
cd frontend
pnpm install
pnpm dev      # dev server
pnpm build    # production build
pnpm lint     # ESLint
pnpm generate:api  # API contract from the OpenAPI document
```

## Project structure

```
frontend/src/
├── api/generated/          # Kubb output. Do not edit
├── domain/                 # One folder for each business area
│   └── [area]/
│       ├── apis/          # API functions, query hooks, and *.keys.ts
│       └── types/         # Types for this area
├── components/
│   ├── ui/                # Shared UI components (shadcn/ui and Base UI)
│   ├── filter-builder/    # QueryKit filter component
│   ├── app-sidebar.tsx    # Main navigation
│   ├── theme-context.ts   # Theme context and useTheme
│   ├── theme-provider.tsx # Theme provider
│   └── theme-toggle.tsx   # Light, dark, and system toggle
├── routes/                 # TanStack Router file routes
├── hooks/                  # Shared React hooks
├── lib/                    # api-client.ts, problem-details.ts, utils.ts
└── main.tsx               # App entry point
```

## API client

`lib/api-client.ts` exports `apiClient`, an Axios instance for the BFF:

- `withCredentials: true` sends the BFF cookie.
- The `X-CSRF: 1` header is necessary for the BFF CSRF protection.
- An error response that has problem details becomes a `ProblemDetailsError`. The client shows it as a toast by default. To stop the toast, send `{ suppressErrorToast: true }` with the request.

The generated API clients send each request through `apiClient`. `main.tsx` imports `lib/api-client.ts` to connect them before the first request. Use `apiClient` directly only for endpoints that are not in the OpenAPI document, such as `/bff/user`.

## API contract

Kubb generates the API contract from the OpenAPI document of the Server. The configuration is in `kubb.config.ts`. The output is in `src/api/generated/`, with one file for each endpoint:

```
src/api/generated/
├── [tag]/
│   ├── clients/[operation].ts  # Axios client function
│   ├── types/[operation].ts    # Request and response types
│   └── zod/[operation].ts      # Zod schemas
├── models/
│   ├── types/[schema].ts       # Shared DTO types
│   └── zod/[schema].ts         # Shared DTO Zod schemas
└── .kubb/                      # Client runtime
```

The tag and the operation names come from the endpoint names (`Name =` or `.WithName`) and the controller tags. The Server build fails if an endpoint has no name.

To change the contract after a change to the Server API:

1. Build the Server in Debug. The build writes `FullstackTemplate.Server/openapi/FullstackTemplate.Server.json`.
2. Run `pnpm generate:api` in `frontend/`.
3. Commit the OpenAPI document and the generated files together.

If a generated file has a merge conflict, do not merge it by hand. Accept either side, then do steps 1 and 2 again. The `API contract` CI workflow fails when the committed files are not current. See [ADR 0001](adr/0001-commit-generated-api-contract.md).

Do not edit the generated files. ESLint ignores them, but `tsc` compiles them.

- A list client (an operation name that ends in `List`) returns the full response, because the paging metadata is in the `X-Pagination` header. Parse the header into the `PaginationMetadata` type.
- The other clients return only the response body.
- The Zod schemas are for form validation and tests. The clients do not parse responses with them.

```typescript
import { getTenant } from '@/api/generated/tenants/clients/get-tenant'
import type { TenantDto } from '@/api/generated/models/types/tenant-dto'

const tenant: TenantDto = await getTenant({ path: { id }, signal })
```

## Query hooks

Each area keeps its hook and its query key factory in `domain/[area]/apis/`. The hook calls the generated client. Write the hooks and the keys by hand.

```typescript
import { useWeatherForecast } from '@/domain/weather/apis/get-weather'
import { useAuth } from '@/domain/auth/apis/get-user'
import { useAuthActions } from '@/domain/auth/apis/auth-actions'

const { data, isLoading, error } = useWeatherForecast()
const { isLoggedIn, username, logoutUrl } = useAuth()
const { login, logout } = useAuthActions()
```

Never copy server state into local state. See `.claude/rules/frontend/async-server-state-management.md`.

## Routes

Each file in `routes/` is a route. The router generates `routeTree.gen.ts`. Do not edit that file.

```typescript
import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/users')({
  component: UsersPage,
})
```

## Theme

`useTheme()` from `@/components/theme-context` returns `theme` and `setTheme`. The values are `'light'`, `'dark'`, and `'system'`. The theme persists in localStorage.

## Filter builder

`FilterBuilder` makes filter strings for QueryKit list endpoints.

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
    // "firstName == John && status ^^ [active]"
  }}
/>
```

The control types are text, number, date, boolean, and multiselect. Groups can use AND or OR.

## Forms

Use React Hook Form with a Zod schema:

```typescript
const schema = z.object({
  name: z.string().min(1, 'Required'),
  email: z.string().email(),
})

const form = useForm({
  resolver: zodResolver(schema),
  defaultValues: { name: '', email: '' },
})
```
