# Client state

Client state is UI state that the API does not own: a dialog that is open, a selected tab, a filter, or a draft. Server state is in [server-state.md](server-state.md).

## Where to put state

Use the first item in this list that fits:

1. Can you calculate the value from props, state, or query data? Calculate it during render. Do not store it.
2. Is it form input? Use React Hook Form. See [frontend.md](../frontend.md#forms).
3. Is it one value in one component? Use `useState`.
4. Do some values always change together? Make a custom hook with `useReducer`.
5. Must the state survive a reload, or be in a link? Put it in the route search params.
6. Do components in different parts of the tree need it? Use a React context. Keep the context small.

The app has no global store library. Before you add one, make sure that items 1 to 6 do not fit.

## Calculate, do not store

```typescript
// Wrong
const [activeUsers, setActiveUsers] = useState<UserDto[]>([])
useEffect(() => {
  setActiveUsers(users.filter((u) => u.isActive))
}, [users])

// Correct
const activeUsers = users.filter((u) => u.isActive)
```

## State that changes together

```typescript
function useTableState() {
  const [state, dispatch] = useReducer(tableReducer, initialState)

  return {
    ...state,
    setPage: (page: number) => dispatch({ type: 'SET_PAGE', page }),
    setSort: (sort: SortConfig) => dispatch({ type: 'SET_SORT', sort }),
    reset: () => dispatch({ type: 'RESET' }),
  }
}
```

Model async state as one union, not as three flags:

```typescript
type AsyncState<T> =
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'success'; data: T }
  | { status: 'error'; error: string }
```

## Effects

Use `useEffect` only to synchronize with a system outside React: a browser API, a subscription, a timer, or a third-party library.

1. Put each reactive value that the effect reads in the dependency array. Do not disable the `react-hooks` lint rule.
2. Return a cleanup that undoes the setup.
3. Give each concern its own effect.

```typescript
useEffect(() => {
  const handler = (e: KeyboardEvent) => {
    if (e.key === 'Escape') setIsOpen(false)
  }
  document.addEventListener('keydown', handler)
  return () => document.removeEventListener('keydown', handler)
}, [])
```

Do not use an effect for these jobs:

| Job | Use |
|-----|-----|
| Transform data for render | Calculate it during render |
| Respond to a click or a submit | The event handler |
| Reset state when a prop changes | A `key` on the component |
| Fetch data | TanStack Query |
| Update state after another state update | Do both updates in one event handler |

## Memoization

Use `useMemo` and `useCallback` only for one of these reasons:

- A calculation takes more than 1 ms. Measure it with `console.time` first.
- A child in `React.memo` needs a stable prop.
- A query `select` function needs a stable reference.

Do not memoize a small object or a short `map`.

## Rare hooks

- `useLayoutEffect`: only when you must measure the DOM before the browser paints, for example to put a popover in position.
- `useImperativeHandle`: only for `focus`, `scrollIntoView`, or an imperative library. Prefer a prop such as `open`.
