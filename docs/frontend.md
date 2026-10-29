# Frontend

The frontend is a React 19 SPA in `frontend/`. It uses TanStack Router, TanStack Query, Tailwind v4, and pnpm. The rules are in `docs/frontend/`:

- [server-state.md](frontend/server-state.md): TanStack Query, query keys, mutations, and forms with server data
- [client-state.md](frontend/client-state.md): where to put UI state, effects, and memoization
- [code-style.md](frontend/code-style.md): TypeScript, components, styles, files, and imports
- [testing.md](frontend/testing.md): the test suites, mocks, builders, and selectors

The text of each toast, alert, label, and message follows [messages.md](messages.md).

## Commands

```bash
cd frontend
pnpm install
pnpm dev      # dev server
pnpm build    # production build
pnpm lint     # ESLint
pnpm test     # Vitest: unit tests (src/**/*.test.ts, e2e/*.test.ts) in Node, component tests (src/**/*.test.tsx) in headless Chromium
pnpm test:e2e # Playwright: the functional suite (tests/), the SPA against a mocked BFF. See docs/frontend/testing.md
pnpm generate:api  # API contract from the OpenAPI document
```

The component tests use Vitest browser mode with the Playwright provider. Before the first `pnpm test`, install Chromium once with `pnpm exec playwright install chromium`. A component test renders with `render` from `vitest-browser-react`. Find an element in an overlay with `page` from `vitest/browser`, because an overlay renders outside the test container.

## Project structure

```
frontend/src/
├── api/generated*/         # Kubb output. Do not edit
├── domain/                 # One folder for each business area
│   └── [area]/
│       ├── apis/          # API functions, query hooks, and *.keys.ts
│       ├── components/    # Components for this area
│       └── types/         # Types for this area
├── components/
│   ├── ui/                # Shared UI components (shadcn/ui, React Aria, and Animate UI)
│   ├── filter-builder/    # QueryKit filter component
│   ├── app-sidebar.tsx    # Main navigation
│   ├── theme-context.ts   # Theme context and useTheme
│   ├── theme-provider.tsx # Theme provider
│   └── theme-toggle.tsx   # Light, dark, and system toggle
├── routes/                 # TanStack Router file routes
├── hooks/                  # Shared React hooks
├── lib/                    # api-client.ts, pagination.ts, problem-details.ts, utils.ts
└── main.tsx               # App entry point
```

In `components/ui/`, the variants are in `*-variants.ts` files (for example `button-variants.ts`) and the hooks are in `use-*.ts` files. A component from `shadcn add` imports them from the component file, for example `buttonVariants` from `@/components/ui/button`. Change that import to the new file.

Wrap the `cva` call of a `*-variants.ts` file in `withMergedClasses` from `@/lib/utils`. Then a direct call, such as `<Link className={buttonVariants({ variant: "outline" })}>`, gets the classes that the component gets. Without the wrapper, a base class and a variant class for the same CSS property both get to the element, and the order of the CSS rules selects the class that applies.

## API client

`lib/api-client.ts` exports `apiClient`, an Axios instance for the BFF:

- `withCredentials: true` sends the BFF cookie.
- The `X-CSRF: 1` header is necessary for the BFF CSRF protection.
- An error response that has problem details becomes a `ProblemDetailsError`. The client shows it as a toast by default. To stop the toast, send `{ suppressErrorToast: true }` with the request.

The generated API clients send each request through `apiClient`. `main.tsx` imports `lib/api-client.ts` to connect them before the first request. Use `apiClient` directly only for endpoints that are not in the OpenAPI document, such as `/bff/user`.

A generated client has two error modes. The `throwOnError` option selects the mode:

- `throwOnError: true` (the default): a non-2xx response goes through the `apiClient` error interceptor. The client shows the toast and throws the `ProblemDetailsError`.
- `throwOnError: false`: Kubb sets `validateStatus: () => true`, so axios accepts all statuses. The interceptor does not run. There is no toast and no `ProblemDetailsError`. The client returns the full result. Read `result.status`, and read `result.error` for a non-2xx body.

In both modes, a network error or a cancel still rejects the promise.

```typescript
const result = await getTenant({ path: { id }, throwOnError: false, signal })
if (result.status === 404) {
  // Show the not found state. result.error has the problem details body.
}
```

## API contract

Kubb generates the API contract from the OpenAPI document of the Server. The configuration is in `kubb.config.ts`. The output for v1 is in `src/api/generated/`, and each later version has its own folder, for example `src/api/generated-v2/`. Each folder has one file for each endpoint:

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

The tag and the operation names come from the endpoint names (`.WithName`) and the endpoint tags (`.WithTags`). The Server Debug build fails if an endpoint has no name.

To change the contract after a change to the Server API:

1. Build the Server in Debug with `dotnet build FullstackTemplate.Server --no-incremental`. The build writes `FullstackTemplate.Server/openapi/FullstackTemplate.Server.json`. Without `--no-incremental`, a build with nothing to compile does not write the document again.
2. Run `pnpm generate:api` in `frontend/`.
3. Commit the OpenAPI document and the generated files together.

The `api-contract` skill gives the full procedure and the checks.

If a generated file has a merge conflict, do not merge it by hand. Accept either side, then do steps 1 and 2 again. The `api-contract` job of the `CI` workflow fails when the committed files are not current. See [ADR 0001](adr/0001-commit-generated-api-contract.md).

Do not edit the generated files. ESLint ignores them, but `tsc` compiles them.

- A list client (an operation name that ends in `List`) returns the full response, because the paging metadata is in the `X-Pagination` header. Read the header with `parsePagination` from `lib/pagination.ts`. It returns the `PaginationMetadata` type.
- The other clients return only the response body.
- The Zod schemas are for form validation and tests. The clients do not parse responses with them.

```typescript
import { getTenant } from '@/api/generated/tenants/clients/get-tenant'
import type { TenantDto } from '@/api/generated/models/types/tenant-dto'

const tenant: TenantDto = await getTenant({ path: { id }, signal })
```

```typescript
import { getTenantList } from '@/api/generated/tenants/clients/get-tenant-list'
import { parsePagination } from '@/lib/pagination'

const result = await getTenantList({ query: { pageNumber: 1, pageSize: 10 }, signal })
const tenants = result.data
const pagination = parsePagination(result)
```

### A generated type is wrong

Fix the cause on the Server: the DTO, an attribute such as `[NamesOf<T>]`, or a transformer in `FullstackTemplate.Server/Resources/Entrypoints/Http/OpenApi/`. Add a test in `OpenApiTransformerTests` or `OpenApiDocumentTests`, then regenerate. The rules of the transformers are in [backend.md](backend.md#openapi-document).

| The generated type | The cause on the Server |
|---|---|
| A property is optional, but the DTO property is not nullable | `RequireNonNullablePropertiesTransformer` |
| A number field also takes a string | `RemoveStringFromNumbersTransformer` |
| A `uint` field has no range | `UnsignedIntegerRangeTransformer` |
| A field of names is a `string`, not a union of the names | `[NamesOf<T>]` on the DTO property, and `FixedNamesTransformer` |
| A request field has no max length, no minimum, or no maximum | `[SchemaMaxLength]`, `[SchemaMinimum]`, or `[SchemaMaximum]` on the request DTO property, and `SchemaLimitsTransformer` |
| A request body or a response has a type for each media type | `JsonContentTypeTransformer` |
| A list client has no type for the `X-Pagination` header | `PaginationHeaderTransformer` |
| A secured operation has no 401 response | `UnauthorizedResponseTransformer` |

## Query hooks

Each area keeps its hook and its query key factory in `domain/[area]/apis/`. The hook calls the generated client. Write the hooks and the keys by hand.

```typescript
import { useCurrentUser } from '@/domain/users/apis/get-current-user'
import { useAuth } from '@/domain/auth/apis/get-user'
import { useAuthActions } from '@/domain/auth/apis/auth-actions'

const { data, isLoading, error } = useCurrentUser()
const { isLoggedIn, username, logoutUrl } = useAuth()
const { login, logout } = useAuthActions()
```

Never copy server state into local state. See [server-state.md](frontend/server-state.md).

## Routes

Each file in `routes/` is a route. The router generates `routeTree.gen.ts`. Do not edit that file.

```typescript
import { createFileRoute } from '@tanstack/react-router'

export const Route = createFileRoute('/users')({
  component: UsersPage,
  staticData: { breadcrumb: 'Users' },
})
```

Each page route sets `staticData.breadcrumb`. The layout header shows it as the name of the page. A test in `src/root-route.test.tsx` fails when a page route has no breadcrumb.

## Links

To link to a route, use `Link` from `@/components/ui/link`. Give the path of the route in `to`, and give its params in `params`. TypeScript checks both against the route tree:

```typescript
import { Link } from '@/components/ui/link'

<Link to="/users/$id" params={{ id: user.id }}>{user.fullName}</Link>
```

- Do not build the path as a string, such as `` `/users/${user.id}` ``. TypeScript does not check a string path.
- `search`, `hash`, `replace`, and the other options of the TanStack Router `Link` also work.
- The link is a React Aria `Link`: use `onPress`, not `onClick`. A click with Cmd, Ctrl, or Shift, and a middle-click, open the link in a new tab or window.
- The link sets `aria-current="page"` only when it goes to the current page, with the same search params.
- To disable a link, set `disabled`.
- `BreadcrumbLink`, `SidebarMenuLink`, and `SidebarMenuSubLink` take the same props.
- `SidebarMenuLink` and `SidebarMenuSubLink` set `aria-current="page"` from their `isActive` prop, not from the router.
- To make another link component, render `RouterLinkPrimitive` in it and wrap it with `createRouterLink`. See `BreadcrumbLink` in `components/ui/breadcrumb.tsx`.

ESLint does not let you import `Link` from `react-aria-components`, `@animate-ui/react/primitives/react-aria`, or `@tanstack/react-router`.

Other React Aria components take a plain `href`, for example `MenuItem` and `Tab`. The root route wraps the app in `HrefRouterProvider` from `@/components/ui/link`, so the router goes to the `href` with no page load. TypeScript does not check a plain `href`, so for a link to a route, prefer a typed wrapper made with `createRouterLink`. An `href` that has no route in the SPA, such as `/bff/login`, loads the page from the server.

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

The control types are text, number, date, boolean, and multiselect. Groups can use AND or OR. For a date of a place, give the config the `timeZone` of the place. See "Dates and times" in [code-style.md](frontend/code-style.md).

<!-- example:start -->
"Starts At in Pago Pago" in `routes/(examples)/filter-demo.tsx` gives the `timeZone` of a place.
<!-- example:end -->

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

Take each limit of a form field from the generated Zod schema of its DTO, not from a copy of the number. `maxLengthOf`, `minimumOf`, and `maximumOf` in `lib/api-contract.ts` read the limit. For a text field, use `stringWithMaxLengthOf`, not `z.string().max()`. The Server counts code points, but `.max()` counts UTF-16 units and refuses text that the Server takes. These functions throw when the contract has no limit. To fix that, add `[SchemaMaxLength]`, `[SchemaMinimum]`, or `[SchemaMaximum]` to the DTO on the Server, then regenerate the contract (see [API contract](#api-contract)). A `uint` field always has a minimum and a maximum in the contract, so `minimumOf` and `maximumOf` do not catch a missing `[SchemaMinimum]` or `[SchemaMaximum]` on a `uint`.

```typescript
const firstName = userForCreationDtoSchema.shape.firstName
const schema = z.object({ firstName: stringWithMaxLengthOf(firstName).min(1, 'Required') })
```
