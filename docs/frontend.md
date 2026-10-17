# Frontend

The frontend is a React 19 SPA in `frontend/`. It uses TanStack Router, TanStack Query, Tailwind v4, and pnpm. The detailed rules are in `.claude/rules/frontend/`.

## Commands

```bash
cd frontend
pnpm install
pnpm dev      # dev server
pnpm build    # production build
pnpm lint     # ESLint
```

## Project structure

```
frontend/src/
├── domain/                 # One folder for each business area
│   └── [area]/
│       ├── apis/          # API functions, query hooks, and *.keys.ts
│       └── types/         # Types for this area
├── components/
│   ├── ui/                # Shared UI components (shadcn/ui and Base UI)
│   ├── filter-builder/    # QueryKit filter component
│   ├── app-sidebar.tsx    # Main navigation
│   ├── theme-provider.tsx # Theme context
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

```typescript
import { apiClient } from '@/lib/api-client'

const response = await apiClient.get('/api/v1/users')
```

## Query hooks

Each area keeps its API function, its hook, and its query key factory in `domain/[area]/apis/`.

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

`useTheme()` from `@/components/theme-provider` returns `theme` and `setTheme`. The values are `'light'`, `'dark'`, and `'system'`. The theme persists in localStorage.

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
