# Server state

Server state is the data that the API returns. TanStack Query owns it. It caches the data and fetches the data again when it becomes stale. The examples come from `frontend/src/domain/users/apis/`.

## Rules

1. Use TanStack Query for all API data.
2. Never copy server state into `useState`, a context, or a store. Read it from the query each time.
3. Put all the values that the query function uses in the query key.
4. Make the query keys with a key factory in `[area].keys.ts`.
5. Call the generated client in a hand-written hook in `domain/[area]/apis/`. Do not write an API function or a DTO type by hand. Kubb generates them. See [frontend.md](../frontend.md#api-contract).
6. After a mutation, invalidate each query that the mutation changes.
7. Show the loading state and the error state of each query.
8. Show what the Server allows. Do not copy a state machine, a permission check, or a business limit. To show a button for an action, read `allowedActions` or a flag of the DTO. If the DTO does not have the answer, add it on the Server (rule 11 of [dtos-and-mappings.md](../backend/dtos-and-mappings.md)). `domain/users/components/user-actions.tsx` shows a button for each allowed action of a user.

Check: `pnpm lint` fails on these forms. The rules are in `frontend/eslint.config.js`.

- Rule 2 (`server-state/no-copied-state`): a `useState` whose first value is a prop or the result of a hook such as `useQuery`, also through a field, `?.`, `??`, `? :`, or a local `const`. A prop whose name starts with `initial` or `default` is a first value that the user owns from then on, so it passes. A value that a call computes, such as `useState(() => parse(value))`, passes too. No check finds server data that a call copies, or a copy into a context.
- Rule 3 (`server-state/query-key-has-inputs`): a local value that the `queryFn` reads and the `queryKey` does not, in `useQuery`, `queryOptions`, `useInfiniteQuery`, and the other query calls. It compares variables, not fields, so a key with `params` covers `params.page`. A value in the `options` of the client, such as `suppressErrorToast`, does not change the data, so it passes.
- Rule 3 in a key factory (`server-state/key-has-each-param`): in a `*.keys.ts` file, a parameter that the key does not use whole, such as `(params) => [..., params.search]`.

<!-- example:start -->
In the workshop example, the detail page shows a button only for each entry of `allowedActions`: [`WorkshopActions`](<../../frontend/src/examples/workshops/components/workshop-actions.tsx>). A `Record` of the generated union gives each action its label, so a new action on the Server fails the build until it has one: [`LABELS`](<../../frontend/src/examples/workshops/components/workshop-actions.tsx>).
<!-- example:end -->

## Key factory

Order the key parts from the most general to the most specific. Then you can invalidate one item, one list, or the whole area.

```typescript
export const UserKeys = {
  all: ['users'] as const,
  lists: () => [...UserKeys.all, 'list'] as const,
  list: (filters: string) => [...UserKeys.lists(), { filters }] as const,
  details: () => [...UserKeys.all, 'detail'] as const,
  detail: (id: string) => [...UserKeys.details(), id] as const,
}
```

<!-- example:start -->
The workshop example puts the search and the page in the list key: [`WorkshopKeys`](<../../frontend/src/examples/workshops/apis/workshop.keys.ts>).
<!-- example:end -->

## Query

```typescript
import { useQuery } from '@tanstack/react-query'

import { getUser } from '@/api/generated/users/clients/get-user'

import { UserKeys } from './user.keys'

export function useUser(id: string | undefined) {
  return useQuery({
    queryKey: UserKeys.detail(id ?? ''),
    queryFn: ({ signal }) => getUser({ path: { id: id! }, signal }),
    enabled: !!id,
  })
}
```

Give the `signal` to the client, so that TanStack Query can cancel the request.

When more than one place uses the same query, put the key, the function, and the options in one `queryOptions` object. Then `useQuery`, `useSuspenseQuery`, and `queryClient.prefetchQuery` can share it.

<!-- example:start -->
The workshop example has one `queryOptions` for the detail: [`workshopQueryOptions`](<../../frontend/src/examples/workshops/apis/get-workshop.ts>). The list keeps the last page on the screen while the next one loads: [`useWorkshopList`](<../../frontend/src/examples/workshops/apis/get-workshop-list.ts>). The venue picker loads its search one page at a time with `useInfiniteQuery`: [`useVenueSearch`](<../../frontend/src/examples/venues/apis/get-venue-list.ts>).
<!-- example:end -->

## Mutation

```typescript
export function useUpdateUser() {
  const queryClient = useQueryClient()

  return useMutation({
    mutationFn: ({ id, body }: { id: string; body: UserForUpdateDto }) => updateUser({ path: { id }, body }),
    onSuccess: (_, { id }) => {
      queryClient.invalidateQueries({ queryKey: UserKeys.detail(id) })
      queryClient.invalidateQueries({ queryKey: UserKeys.lists() })
    },
  })
}
```

For an optimistic update, change only the query cache. In `onMutate`, cancel the queries, keep the old data, and set the new data. In `onError`, put back the old data. In `onSettled`, invalidate the queries.

<!-- example:start -->
In the workshop example, a save puts the answer of the Server in the detail cache and invalidates the lists: [`useUpdateWorkshop`](<../../frontend/src/examples/workshops/apis/update-workshop.ts>). A refused action reads the workshop again, because another user can have changed it: [`useSendWorkshopAction`](<../../frontend/src/examples/workshops/apis/send-workshop-action.ts>).
<!-- example:end -->

## Errors

`apiClient` shows a toast for each problem details error. See [frontend.md](../frontend.md#api-client). A component does not have to show the same error again. It must still show a state for a failed query, so that the page is not empty.

A generated client rejects with a `ProblemDetailsError` when the Server returns problem details. Do not wrap the client to change the error. To read the status, use `isProblemDetailsError` from `@/lib/problem-details`:

```typescript
retry: (failureCount, error) => {
  // A 404 does not change on a retry
  if (isProblemDetailsError(error) && error.status === 404) {
    return false
  }
  return failureCount < 3
},
```

<!-- example:start -->
A form that shows each refusal itself sends `suppressErrorToast`, so the error does not show twice: [`useUpdateWorkshop`](<../../frontend/src/examples/workshops/apis/update-workshop.ts>).
<!-- example:end -->

`useServerErrors` shows a refusal in the form: each validation message on its field, a stale version as its own problem, and every other message in `RefusedSaveAlert` above the buttons. See [`refusalOf`](../../frontend/src/hooks/use-server-errors.ts) and [`RefusedSaveAlert`](../../frontend/src/components/refused-save-alert.tsx).

## Cache times

`main.tsx` makes the `QueryClient` with `retry: 1` and `refetchOnWindowFocus: false`. The default `staleTime` is 0. Set a `staleTime` on a query when its data changes slowly. Use `Infinity` for data that does not change while the app runs.

## Forms with server data

Do not copy the data into state. Give it to the form. An edit form also keeps the `version` of the record that it shows, and sends it in the PUT body:

```typescript
// The parent renders this form only after useUser has data
const form = useForm<UserFormData>({
  resolver: zodResolver(userSchema),
  defaultValues: { firstName: user.firstName, lastName: user.lastName, version: user.version },
})
```

Use `defaultValues` in an edit form, not `values`. With `values`, a refetch can put the new version under the edits of the user. The save then overwrites a change that the user did not see.

When another user saved the record first, the PUT gets a 409 with the type `urn:fullstacktemplate:problem:stale-version`, and the toast shows the `detail`. A create can also get this type, when a unique index of the Server tells that another user changed the record. For a create, keep the input of the user and refetch the list or the parent record. Find a stale save by `error.problemDetails.type === 'urn:fullstacktemplate:problem:stale-version'` on the `ProblemDetailsError`, not by the status alone, because a 409 can also be a duplicate value. Keep the edits of the user in the form. Give a way to load the record again, for example a button that refetches the detail query and resets the form with the new data. Do not send the PUT again with the new version, because that overwrites the other change.

<!-- example:start -->
In the workshop example, the edit form keeps the loaded version in `defaultValues`: [`WorkshopEditForm`](<../../frontend/src/examples/workshops/components/workshop-edit-form.tsx>). `isStaleVersionError` reads the type: [`isStaleVersionError`](<../../frontend/src/lib/problem-details.ts>). `Load the latest` refetches the detail and resets the form, on the same page: [`handleLoadLatest`](<../../frontend/src/examples/workshops/components/workshop-edit-form.tsx>) and [`EditWorkshopPage`](<../../frontend/src/routes/(examples)/workshops/$id/edit.tsx>). The form shows the problem itself, so this save sends no toast.
<!-- example:end -->

## Do not

```typescript
// Wrong: copies server state
const { data } = useUser(userId)
const [user, setUser] = useState(data)

// Wrong: fetches in an effect
useEffect(() => {
  getUser({ path: { id: userId } }).then(setUser)
}, [userId])

// Wrong: copies the permission rule of the Server
const canDelete = me.permissions.includes('manage_users')

// Right: reads the answer of the Server
const canDelete = user.allowedActions.includes('Delete')
```
