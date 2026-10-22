# Server state

Server state is the data that the API returns. TanStack Query owns it. It caches the data and fetches the data again when it becomes stale. The examples come from `frontend/src/domain/weather/apis/`.

## Rules

1. Use TanStack Query for all API data.
2. Never copy server state into `useState`, a context, or a store. Read it from the query each time.
3. Put all the values that the query function uses in the query key.
4. Make the query keys with a key factory in `[area].keys.ts`.
5. Call the generated client in a hand-written hook in `domain/[area]/apis/`. Do not write an API function or a DTO type by hand. Kubb generates them. See [frontend.md](../frontend.md#api-contract).
6. After a mutation, invalidate each query that the mutation changes.
7. Show the loading state and the error state of each query.

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

When another user saved the record first, the PUT gets a 409 with `errors.version`, and the toast shows the `detail`. Keep the edits of the user in the form. Give a way to load the record again, for example a button that refetches the detail query and resets the form with the new data. Do not send the PUT again with the new version, because that overwrites the other change.

## Do not

```typescript
// Wrong: copies server state
const { data } = useUser(userId)
const [user, setUser] = useState(data)

// Wrong: fetches in an effect
useEffect(() => {
  getUser({ path: { id: userId } }).then(setUser)
}, [userId])
```
