---
description: Rules for managing server state, API calls, and async operations with React Query
globs: src/**/*.{ts,tsx}
alwaysApply: true
---

# Async State, Server State, and API Call Management Rules

## Core Principles

### 1. Server State vs Client State Separation
**CRITICAL**: Never copy server state to client state. Server state is "borrowed" data that belongs to the backend - treat it as such.

- **Server State**: Data fetched from APIs (patients, accessions, samples, etc.)
  - Managed exclusively by TanStack Query
  - Cached and synchronized automatically
  - Never duplicated into local state
  - Is a "snapshot in time" that can become outdated
  - Requires automatic background synchronization
- **Client State**: UI-specific state only (modal open/closed, form drafts, filters)
  - Managed by Zustand stores or component state
  - Limited to truly local concerns
  - Persists across server state updates

**Mental Model**: TanStack Query is an async state manager, not just a data fetching library. Think of it as managing the lifecycle of server state, not just transferring data.

### 2. TanStack Query Patterns

#### Query Keys
Query keys must be treated like dependency arrays - include all variables that affect the query:

```tsx
// ✅ CORRECT - All dependencies in the query key
useQuery({
  queryKey: ['patients', patientId, { includeRelationships }],
  queryFn: () => getPatient(patientId, { includeRelationships })
})

// ❌ WRONG - Missing dependencies
useQuery({
  queryKey: ['patients'],
  queryFn: () => getPatient(patientId)
})
```

#### Query Key Factory Pattern
Use the established key factory pattern for consistency. Structure keys from most generic to most specific:

```tsx
const PatientKeys = {
  all: ["Patients"] as const,
  lists: () => [...PatientKeys.all, "list"] as const,
  list: (queryParams: string) => [...PatientKeys.lists(), { queryParams }] as const,
  details: () => [...PatientKeys.all, "detail"] as const,
  detail: (patientId: string) => [...PatientKeys.details(), patientId] as const,
};
```

**Key Principles:**
- Always use array keys (never strings)
- Structure from generic to specific for granular invalidation
- Colocate query keys with their feature code
- Use `as const` for type safety

### 3. API Hook Implementation

#### Query Hooks
```tsx
export const usePatient = (patientId: string, options?: UseQueryOptions) => {
  return useQuery({
    queryKey: PatientKeys.detail(patientId),
    queryFn: () => getPatient(patientId),
    // Set appropriate staleTime based on data volatility
    staleTime: 5 * 60 * 1000, // 5 minutes
    ...options
  });
};
```

#### Mutation Hooks with Smart Invalidation
```tsx
export function useUpdatePatient(options?: UseMutationOptions) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: ({ id, data }) => updatePatient(id, data),
    onSuccess: (_, { id }) => {
      // Invalidate specific queries affected by this mutation
      queryClient.invalidateQueries({ queryKey: PatientKeys.detail(id) });
      queryClient.invalidateQueries({ queryKey: PatientKeys.lists() });
      // Cross-domain invalidations when necessary
      queryClient.invalidateQueries({ queryKey: AccessionKeys.forEdits() });
    },
    ...options
  });
}
```

### 4. State Management Anti-Patterns to Avoid

#### ❌ NEVER: Copy Server State to Local State
```tsx
// ❌ WRONG - Copying server state
const { data: patient } = usePatient(patientId);
const [localPatient, setLocalPatient] = useState(patient);

// ❌ WRONG - Syncing server state with useEffect
useEffect(() => {
  setFormData(serverData);
}, [serverData]);
```

#### ✅ CORRECT: Use Server State Directly
```tsx
// ✅ CORRECT - Use server state directly
const { data: patient } = usePatient(patientId);

// ✅ CORRECT - For forms, use defaultValues
const form = useForm({
  defaultValues: patient,
});

// ✅ CORRECT - Or use reset when data loads
useEffect(() => {
  if (patient) {
    form.reset(patient);
  }
}, [patient, form.reset]);
```

### 5. Zustand Store Patterns

Zustand stores should only manage UI state and query parameters, never server data:

```tsx
// ✅ CORRECT - Store only manages UI state
export const useAccessioningWorklistTableStore = create((set, get) => ({
  // UI State
  pageNumber: 1,
  pageSize: 10,
  sorting: [],
  status: [], // Filter state
  filterInput: null,
  
  // Query helper
  queryKit: {
    filterValue: () => {
      // Constructs filter string for API
    }
  }
}));

// ❌ WRONG - Store contains server data
export const usePatientStore = create((set) => ({
  patients: [], // ❌ Server data doesn't belong here
  setPatients: (patients) => set({ patients })
}));
```

### 6. Optimistic Updates

Only manipulate the query cache for optimistic updates:

```tsx
export function useAddSample(options?: UseMutationOptions) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: addSample,
    onMutate: async (newSample) => {
      // Cancel in-flight queries
      await queryClient.cancelQueries({ queryKey: SampleKeys.lists() });
      
      // Snapshot previous value
      const previousSamples = queryClient.getQueryData(SampleKeys.lists());
      
      // Optimistically update
      queryClient.setQueryData(SampleKeys.lists(), (old) => [...old, newSample]);
      
      return { previousSamples };
    },
    onError: (err, newSample, context) => {
      // Rollback on error
      queryClient.setQueryData(SampleKeys.lists(), context.previousSamples);
    },
    onSettled: () => {
      // Always refetch after mutation
      queryClient.invalidateQueries({ queryKey: SampleKeys.lists() });
    }
  });
}
```

### 7. Form Handling with Server Data

For forms that edit server data:

```tsx
function PatientEditForm({ patientId }: { patientId: string }) {
  const { data: patient, isLoading } = usePatient(patientId);
  const updatePatient = useUpdatePatient();
  
  const form = useForm<PatientFormData>({
    resolver: zodResolver(patientSchema),
    // Don't set defaultValues here - wait for data
  });

  // Reset form when server data loads
  useEffect(() => {
    if (patient) {
      form.reset({
        firstName: patient.firstName,
        lastName: patient.lastName,
        // ... map server data to form shape
      });
    }
  }, [patient, form.reset]);

  const onSubmit = (data: PatientFormData) => {
    updatePatient.mutate({
      id: patientId,
      data
    });
  };

  if (isLoading) return <LoadingSpinner />;
  
  return <form onSubmit={form.handleSubmit(onSubmit)}>...</form>;
}
```

### 8. Background Refetching Configuration

Configure appropriate refetching behavior based on data volatility:

```tsx
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 5 * 60 * 1000, // 5 minutes - adjust based on data freshness needs
      gcTime: 10 * 60 * 1000, // 10 minutes (formerly cacheTime)
      refetchOnWindowFocus: true, // Great for production - sync data when user returns
      refetchOnMount: true,
      refetchOnReconnect: true,
      retry: 3,
      // Consider structuralSharing: false for non-JSON data
    },
  },
});
```

**StaleTime Guidelines:**
- `staleTime: 0` (default): Data considered stale immediately, refetches aggressively
- `staleTime: 5 * 60 * 1000`: Good for dynamic data that changes moderately
- `staleTime: Infinity`: For data that rarely changes, prevents background refetches
- Customize per query based on data volatility

### 9. Error Handling

Leverage TanStack Query's built-in error handling:

```tsx
function PatientDetails({ patientId }: { patientId: string }) {
  const { data, isError, error, isLoading } = usePatient(patientId);
  
  if (isLoading) return <LoadingSpinner />;
  
  if (isError) {
    return <ErrorDisplay error={error} />;
  }
  
  return <PatientInfo patient={data} />;
}
```

### 10. TypeScript Patterns

Always type your API responses and use them consistently:

```tsx
// types/index.ts
export interface PatientDto {
  id: string;
  firstName: string;
  lastName: string;
  dateOfBirth: string;
  // ...
}

// apis/get-patient.tsx
const getPatient = async (id: string): Promise<PatientDto> => {
  return peakLimsApi
    .get(`/v1/patients/${id}`)
    .then((response) => response.data as PatientDto);
};
```

### 11. Performance Optimizations

#### Render Optimizations
```tsx
// ✅ Extract stable select functions to avoid re-renders
const selectTodoCount = (data: Todo[]) => data.length;

export const useTodoCount = () => {
  return useTodos({ select: selectTodoCount });
};

// ✅ Or memoize with useCallback
export const useTodoCount = () => {
  return useTodos({
    select: useCallback((data: Todo[]) => data.length, [])
  });
};
```

#### Parallel vs Serial Queries
```tsx
// ✅ Parallel queries - all fetch simultaneously
const usersQuery = useQuery({ queryKey: ['users'], queryFn: fetchUsers });
const teamsQuery = useQuery({ queryKey: ['teams'], queryFn: fetchTeams });

// ❌ Avoid serial suspense queries when possible
const usersQuery = useSuspenseQuery({ queryKey: ['users'], queryFn: fetchUsers });
const teamsQuery = useSuspenseQuery({ queryKey: ['teams'], queryFn: fetchTeams });
```

#### Query Options Helper
```tsx
// ✅ Use queryOptions for shared query configuration
import { queryOptions } from '@tanstack/react-query';

function patientOptions(id: string) {
  return queryOptions({
    queryKey: ['patient', id],
    queryFn: () => fetchPatient(id),
    staleTime: 5 * 60 * 1000,
  });
}

// Usage across different hooks
useQuery(patientOptions(id));
useSuspenseQuery(patientOptions(id));
queryClient.prefetchQuery(patientOptions(id));
```

### 12. Advanced Patterns

#### Prefetching Strategies
```tsx
// ✅ Prefetch on user interaction
function PatientListItem({ patient }) {
  const queryClient = useQueryClient();
  
  const prefetchPatient = () => {
    queryClient.prefetchQuery({
      queryKey: ['patient', patient.id],
      queryFn: () => fetchPatient(patient.id),
      staleTime: 60 * 1000, // Important: set staleTime for prefetching
    });
  };

  return (
    <div onMouseEnter={prefetchPatient} onFocus={prefetchPatient}>
      {patient.name}
    </div>
  );
}

// ✅ Prefetch related data within query function
const useArticle = (id: string) => {
  const queryClient = useQueryClient();
  
  return useQuery({
    queryKey: ['article', id],
    queryFn: async (...args) => {
      // Prefetch related comments
      queryClient.prefetchQuery({
        queryKey: ['article-comments', id],
        queryFn: () => fetchArticleComments(id),
      });
      
      return fetchArticle(...args);
    },
  });
};
```

#### Context Integration (Use Sparingly)
```tsx
// ✅ Only for mandatory, tree-wide data
const CurrentUserContext = React.createContext<User | null>(null);

export const useCurrentUser = () => {
  const user = React.useContext(CurrentUserContext);
  if (!user) {
    throw new Error('useCurrentUser must be used within CurrentUserProvider');
  }
  return user;
};

// Provider handles loading/error states
export const CurrentUserProvider = ({ children }) => {
  const { data: user, isLoading, error } = useQuery({
    queryKey: ['current-user'],
    queryFn: fetchCurrentUser,
  });

  if (isLoading) return <LoadingSpinner />;
  if (error) return <ErrorBoundary />;
  
  return (
    <CurrentUserContext.Provider value={user}>
      {children}
    </CurrentUserContext.Provider>
  );
};
```

## Summary Checklist

- [ ] **NEVER** copy server state to local state
- [ ] Use TanStack Query for all server state management
- [ ] Include all dependencies in query keys
- [ ] Use query key factories for consistency
- [ ] Structure keys from generic to specific
- [ ] Invalidate related queries after mutations
- [ ] Store only UI state in Zustand
- [ ] Use form.reset() for server data in forms
- [ ] Configure appropriate staleTime and gcTime
- [ ] Handle loading and error states properly
- [ ] Type all API responses
- [ ] Extract stable select functions to avoid re-renders
- [ ] Use queryOptions for shared configurations
- [ ] Prefetch strategically on user interactions
- [ ] Avoid serial suspense queries when possible

## References

- [TanStack Query Documentation](https://tanstack.com/query/latest)
- [Practical React Query - tkdodo](https://tkdodo.eu/blog/practical-react-query)
- [Effective React Query Keys - tkdodo](https://tkdodo.eu/blog/effective-react-query-keys)
- [Thinking in React Query - tkdodo](https://tkdodo.eu/blog/thinking-in-react-query)
- [React Query and React Context - tkdodo](https://tkdodo.eu/blog/react-query-and-react-context)
- [Query Options API - tkdodo](https://tkdodo.eu/blog/the-query-options-api)