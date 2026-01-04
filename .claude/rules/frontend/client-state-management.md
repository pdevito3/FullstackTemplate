---
description: Rules and best practices for managing client-side state with React hooks
globs: src/**/*.{ts,tsx}
alwaysApply: true
---

# Client State Management Rules

This document outlines the rules and best practices for managing client-side state in the React application.

## Core Principles

### 1. Built-in React State First
- **Always start with `useState`** for simple, independent state
- Only move to more complex solutions when there's a clear need
- Prefer colocation of state close to where it's used

### 2. Server State Separation
- **NEVER copy server state to client state** - server state is managed by React Query
- Server state includes: API responses, cached data, loading states, error states
- Client state should only contain: UI state, form state, local preferences, temporary values

## State Management Patterns

### useState for Simple State
Use `useState` for:
- Toggle states (modals, dropdowns, expanded states)
- Form input values (when not using React Hook Form)
- Simple UI state that doesn't need to be shared

```typescript
// ✅ Good - simple independent state
const [isOpen, setIsOpen] = useState(false)
const [selectedTab, setSelectedTab] = useState('details')
```

### Custom Hooks with useReducer for Coupled State
When you have **multiple useState calls that need to change together**, create a custom hook backed by `useReducer`:

```typescript
// ✅ Good - coupled state managed together
function useTableState() {
  const [state, dispatch] = useReducer(tableReducer, initialState)
  
  return {
    ...state,
    setPage: (page: number) => dispatch({ type: 'SET_PAGE', payload: page }),
    setPageSize: (size: number) => dispatch({ type: 'SET_PAGE_SIZE', payload: size }),
    setSort: (sort: SortConfig) => dispatch({ type: 'SET_SORT', payload: sort }),
    resetFilters: () => dispatch({ type: 'RESET_FILTERS' }),
  }
}

// ❌ Bad - multiple coupled useState calls
function BadTableState() {
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [sort, setSort] = useState<SortConfig>()
  
  // Problem: these often need to change together
  const resetFilters = () => {
    setPage(1)
    setSort(undefined)
    // Easy to forget to reset all related state
  }
}
```

### Zustand for Global Client State
Use Zustand (already configured) for:
- Global UI state that multiple components need
- User preferences and settings
- Application-wide client state

## React Hook Guidelines

### useEffect - Synchronization Only
`useEffect` is for **synchronizing with external systems**. Ask yourself **WHY** this code needs to run - use Effects only for code that should run **because** the component was displayed to the user. Based on [React docs](https://react.dev/reference/react/useEffect):

#### ✅ Valid useEffect Uses:
- Connecting to external systems (WebSocket, browser APIs, third-party libraries)
- Setting up subscriptions
- Manually changing the DOM (rarely needed)
- Triggering animations
- Analytics/logging (non-user-initiated events)

#### Effect Best Practices:
- **Always include all reactive values in dependencies** - don't suppress ESLint warnings
- **Make cleanup symmetrical to setup** - stop or undo whatever setup did
- **Keep Effects focused and independent** - separate concerns into different Effects
- **Extract complex Effect logic into custom hooks**

```typescript
// ✅ Good - synchronizing with browser API
useEffect(() => {
  const handler = (e: KeyboardEvent) => {
    if (e.key === 'Escape') setIsOpen(false)
  }
  document.addEventListener('keydown', handler)
  return () => document.removeEventListener('keydown', handler)
}, [])

// ✅ Good - synchronizing with external library
useEffect(() => {
  const chart = new Chart(canvasRef.current, config)
  return () => chart.destroy()
}, [])
```

#### ❌ Invalid useEffect Uses:
- **Transforming data for rendering** (calculate during render instead)
- **Handling user events** (use event handlers - they're not reactive)
- **Resetting state when props change** (use `key` prop or derive state)
- **Fetching server state** (use React Query)
- **Chaining state updates** (handle all updates in single event handler)
- **Expensive calculations** (use `useMemo` instead)
- **Sharing logic between event handlers** (extract shared function)

```typescript
// ❌ Bad - data transformation should be outside useEffect
useEffect(() => {
  setFilteredItems(items.filter(item => item.category === selectedCategory))
}, [items, selectedCategory])

// ✅ Good - derive state during render
const filteredItems = items.filter(item => item.category === selectedCategory)

// ❌ Bad - resetting state in effect
useEffect(() => {
  setSelection(null)
}, [items])

// ✅ Good - use key to reset component state
<ItemList key={categoryId} items={items} />

// ❌ Bad - chaining effects that update each other
useEffect(() => {
  if (selectedItem) {
    setRelatedItems(getRelatedItems(selectedItem))
  }
}, [selectedItem])

useEffect(() => {
  if (relatedItems.length > 0) {
    setShowRelated(true)
  }
}, [relatedItems])

// ✅ Good - handle all updates in event handler
const handleItemSelect = (item) => {
  setSelectedItem(item)
  const related = getRelatedItems(item)
  setRelatedItems(related)
  setShowRelated(related.length > 0)
}

// ❌ Bad - fetching server state
useEffect(() => {
  fetchPatients().then(setPatients)
}, [])

// ✅ Good - use React Query
const { data: patients } = useQuery({
  queryKey: patientKeys.all(),
  queryFn: fetchPatients
})
```

### useMemo - Expensive Calculations Only
Use `useMemo` only for **genuinely expensive calculations**. "You should only rely on useMemo as a performance optimization." Don't prematurely optimize every calculation. Based on [React docs](https://react.dev/reference/react/useMemo):

#### What Constitutes "Expensive":
- Operations that take measurably significant time (> 1ms)
- Large array filtering/sorting/transforming
- Complex mathematical computations
- **Test with `console.time()` on slower devices to verify**

#### ✅ Valid useMemo Uses:
```typescript
// ✅ Good - genuinely expensive calculation (measured > 1ms)
const expensiveValue = useMemo(() => {
  console.time('expensive-calc') // Use to measure performance
  const result = processLargeDataset(hugeArray)
  console.timeEnd('expensive-calc')
  return result
}, [hugeArray])

// ✅ Good - expensive filtering/sorting large datasets
const sortedAndFilteredItems = useMemo(() => {
  return items
    .filter(item => item.status === filter)
    .sort((a, b) => a.date.localeCompare(b.date))
}, [items, filter])

// ✅ Good - preventing child re-renders with React.memo
const expensiveProps = useMemo(() => ({
  data: processedData,
  onAction: handleAction
}), [processedData, handleAction])

return <ExpensiveChild {...expensiveProps} />
```

#### ❌ Invalid useMemo Uses:
```typescript
// ❌ Bad - simple object creation
const style = useMemo(() => ({ color: 'red' }), [])

// ❌ Bad - simple array operations
const itemNames = useMemo(() => items.map(item => item.name), [items])

// ✅ Good - just create directly
const style = { color: 'red' }
const itemNames = items.map(item => item.name)
```

### useLayoutEffect - Synchronous DOM Reads/Writes
Use `useLayoutEffect` only when you need to **read layout and synchronously re-render**. Based on [React docs](https://react.dev/reference/react/useLayoutEffect):

```typescript
// ✅ Good - measuring DOM before paint
useLayoutEffect(() => {
  const rect = ref.current.getBoundingClientRect()
  setTooltipPosition({ x: rect.x, y: rect.y })
}, [])

// ✅ Good - preventing visual flicker
useLayoutEffect(() => {
  // Synchronously update styles before paint
  ref.current.style.opacity = isVisible ? '1' : '0'
}, [isVisible])
```

**Warning**: `useLayoutEffect` blocks painting and can hurt performance. "Rendering in two passes and blocking the browser hurts performance." Use `useEffect` unless you specifically need synchronous execution to prevent visual flicker.

#### When useLayoutEffect is Required:
- Measuring element dimensions that affect rendering
- Positioning tooltips/popovers based on layout
- Preventing visual "jumps" or flicker
- Any DOM mutations that must happen before paint

### useImperativeHandle - Rare Imperative APIs
Use `useImperativeHandle` **very rarely** when you need to expose imperative methods. Based on [React docs](https://react.dev/reference/react/useImperativeHandle):

```typescript
// ✅ Rare valid use - exposing focus methods
const Input = forwardRef<InputHandle, InputProps>((props, ref) => {
  const inputRef = useRef<HTMLInputElement>(null)
  
  useImperativeHandle(ref, () => ({
    focus: () => inputRef.current?.focus(),
    blur: () => inputRef.current?.blur(),
    select: () => inputRef.current?.select(),
  }))
  
  return <input ref={inputRef} {...props} />
})
```

**Prefer declarative patterns** over imperative APIs whenever possible. Instead of exposing `{ open, close }` methods, use `isOpen` prop.

#### Valid Use Cases (Limited):
- `focus()`, `blur()`, `select()` for inputs
- `scrollIntoView()` for navigation
- Animation triggers that can't be expressed declaratively
- Integration with imperative third-party libraries

#### Alternatives to Consider:
```typescript
// ❌ Imperative pattern
const modalRef = useRef()
const openModal = () => modalRef.current.open()

// ✅ Declarative pattern
const [isModalOpen, setIsModalOpen] = useState(false)
<Modal isOpen={isModalOpen} onClose={() => setIsModalOpen(false)} />
```

## Critical Effect Patterns

### Dependency Arrays - Include All Reactive Values
**Never suppress ESLint warnings about missing dependencies.** Include all reactive values (props, state, derived values) used inside the Effect:

```typescript
// ❌ Bad - missing dependencies, can cause stale closures
useEffect(() => {
  const timer = setInterval(() => {
    setCount(count + 1) // Stale closure - count never updates
  }, 1000)
  return () => clearInterval(timer)
}, []) // Missing 'count' dependency

// ✅ Good - use functional update to avoid dependency
useEffect(() => {
  const timer = setInterval(() => {
    setCount(c => c + 1) // Always gets latest count
  }, 1000)
  return () => clearInterval(timer)
}, [])

// ✅ Good - include all dependencies when needed
useEffect(() => {
  if (isVisible && data) {
    processData(data, userId)
  }
}, [isVisible, data, userId]) // All reactive values included
```

### Cleanup Functions - Prevent Memory Leaks
**Always clean up subscriptions, timers, and listeners:**

```typescript
// ✅ Good - proper cleanup patterns
useEffect(() => {
  // Subscription cleanup
  const subscription = eventEmitter.subscribe(handleEvent)
  return () => subscription.unsubscribe()
}, [])

useEffect(() => {
  // Timer cleanup
  const timer = setTimeout(handleTimeout, delay)
  return () => clearTimeout(timer)
}, [delay])

useEffect(() => {
  // AbortController for fetch cleanup
  const controller = new AbortController()
  
  fetchData({ signal: controller.signal })
    .then(handleData)
    .catch(error => {
      if (error.name !== 'AbortError') {
        handleError(error)
      }
    })
  
  return () => controller.abort()
}, [])

// ❌ Bad - no cleanup, memory leak
useEffect(() => {
  const timer = setInterval(pollData, 1000)
  // Missing cleanup - timer continues forever
}, [])
```

## Anti-Patterns to Avoid

### 1. Copying Server State to Client State
```typescript
// ❌ Bad - copying React Query data to local state
const { data: patients } = useQuery(patientKeys.all(), fetchPatients)
const [localPatients, setLocalPatients] = useState<Patient[]>([])

useEffect(() => {
  if (patients) {
    setLocalPatients(patients)
  }
}, [patients])

// ✅ Good - use React Query data directly
const { data: patients } = useQuery(patientKeys.all(), fetchPatients)
const displayPatients = patients?.filter(p => p.isActive) ?? []
```

### 2. Overusing useEffect for Data Transformation
```typescript
// ❌ Bad - effect for simple transformation
const [filteredData, setFilteredData] = useState([])
useEffect(() => {
  setFilteredData(data.filter(item => item.active))
}, [data])

// ✅ Good - derive during render
const filteredData = data.filter(item => item.active)
```

### 3. Event Handlers vs Effects Confusion
```typescript
// ❌ Bad - using effect for user-initiated action
useEffect(() => {
  if (shouldSendMessage) {
    sendMessage(message)
    setShouldSendMessage(false)
  }
}, [shouldSendMessage, message])

// ✅ Good - use event handler for user actions
const handleSendClick = () => {
  sendMessage(message)
  // Analytics can stay in effect since it's not user-initiated
}
```

### 4. Primitive Obsession in Reducers
```typescript
// ❌ Bad - separate primitive states that change together
const [isLoading, setIsLoading] = useState(false)
const [error, setError] = useState<string | null>(null)
const [data, setData] = useState<Data | null>(null)

// ✅ Good - cohesive state object
type AsyncState<T> = 
  | { status: 'idle' }
  | { status: 'loading' }
  | { status: 'success'; data: T }
  | { status: 'error'; error: string }

const [asyncState, dispatch] = useReducer(asyncReducer, { status: 'idle' })
```

## Decision Tree

1. **Is this server state?** → Use React Query, never copy to client state
2. **Is this simple, independent UI state?** → Use `useState`
3. **Do multiple state values change together?** → Create custom hook with `useReducer`
4. **Does multiple components need this state?** → Use Zustand
5. **Need to run code because component was displayed?** → Use `useEffect` (with proper cleanup)
6. **Need to handle user interaction?** → Use event handler, not Effect
7. **Need expensive calculation (>1ms)?** → Measure first, then consider `useMemo`
8. **Need to prevent visual flicker?** → Use `useLayoutEffect` (sparingly)
9. **Need imperative API?** → Rarely use `useImperativeHandle`, prefer declarative patterns

## Performance Measurement

Before optimizing, **measure actual performance**:

```typescript
// Measure expensive calculations
function ExpensiveComponent({ data }) {
  const result = useMemo(() => {
    console.time('expensive-calculation')
    const processed = expensiveProcessing(data)
    console.timeEnd('expensive-calculation')
    return processed
  }, [data])
  
  return <div>{result}</div>
}

// Profile in production build for accurate measurements
// Use React DevTools Profiler for component render times
// Test on slower devices to ensure good UX for all users
```

## Examples from Codebase Context

Given this is a LIMS system with forms, tables, and complex data:

```typescript
// ✅ Good - Patient form state (local to component)
function PatientForm() {
  const [isDirty, setIsDirty] = useState(false)
  const [showConfirmDialog, setShowConfirmDialog] = useState(false)
  
  // Use React Hook Form for form state, not useState
  const form = useForm<PatientFormData>()
}

// ✅ Good - Sample table state (multiple coupled values)
function useSampleTableState() {
  return useReducer(sampleTableReducer, {
    page: 1,
    pageSize: 50,
    sort: { field: 'createdAt', direction: 'desc' },
    filters: {},
    selection: new Set(),
  })
}

// ✅ Good - Lab equipment connection (external system)
function useLabEquipmentConnection(equipmentId: string) {
  useEffect(() => {
    const connection = new LabEquipmentWebSocket(equipmentId)
    connection.connect()
    return () => connection.disconnect()
  }, [equipmentId])
}
```

These rules ensure consistent, performant, and maintainable client state management while working harmoniously with React Query for server state.