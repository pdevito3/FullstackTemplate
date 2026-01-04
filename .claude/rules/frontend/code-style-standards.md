---
description: Comprehensive code style standards for consistent, maintainable TypeScript React codebase
globs: src/**/*.{ts,tsx}
alwaysApply: true
---

# Code Style Standards

Comprehensive coding standards to ensure consistency, maintainability, and best practices across the TypeScript React codebase.

## TypeScript Standards

### 1. Async/Await over Promises
**Always use async/await syntax instead of Promise chains to prevent callback hell and improve readability.**

```typescript
// ✅ Good - async/await
async function fetchPatientData(patientId: string): Promise<PatientDto> {
  try {
    const response = await peakLimsApi.get(`/v1/patients/${patientId}`);
    return response.data;
  } catch (error) {
    throw new Error(`Failed to fetch patient: ${error.message}`);
  }
}

// ❌ Bad - promise chains
function fetchPatientData(patientId: string): Promise<PatientDto> {
  return peakLimsApi
    .get(`/v1/patients/${patientId}`)
    .then(response => response.data)
    .catch(error => {
      throw new Error(`Failed to fetch patient: ${error.message}`);
    });
}
```

**Exception**: Simple single-operation promise chains in API functions are acceptable for brevity:
```typescript
// ✅ Acceptable for simple operations
export const getPatient = async (patientId: string) => {
  return peakLimsApi
    .get(`/v1/patients/${patientId}`)
    .then((response: AxiosResponse<PatientDto>) => response.data);
};
```

### 2. Type Definitions - Interfaces vs Types

**Use `interface` for object shapes that may be extended. Use `type` for unions, primitives, and computed types.**

```typescript
// ✅ Good - interface for extensible object shapes
interface PatientFormData {
  id: string;
  firstName: string;
  lastName: string;
  dateOfBirth: Date;
}

interface EditablePatientFormData extends PatientFormData {
  isEditing: boolean;
}

// ✅ Good - type for unions and computed types
type AccessionStatus = 
  | "Draft"
  | "Ready For Testing" 
  | "Testing"
  | "Completed"
  | "Abandoned";

type PatientFormFields = keyof PatientFormData;

// ✅ Good - type for function signatures
type FormSubmitHandler<T> = (values: T) => void | Promise<void>;
```

### 3. Strict Type Safety

**Use strict TypeScript settings. Avoid `any` types. Use proper type guards and assertions.**

```typescript
// ✅ Good - proper typing
interface ApiResponse<TData> {
  data: TData;
  pagination?: PaginationInfo;
}

function processApiResponse<T>(response: ApiResponse<T>): T {
  if (!response.data) {
    throw new Error('Invalid API response');
  }
  return response.data;
}

// ❌ Bad - using any
function processApiResponse(response: any): any {
  return response.data;
}

// ✅ Good - type guards
function isPatientDto(obj: unknown): obj is PatientDto {
  return typeof obj === 'object' 
    && obj !== null 
    && 'id' in obj 
    && 'firstName' in obj;
}
```

### 4. Const Assertions and Enums

**Use const assertions for readonly arrays and objects. Prefer string union types over enums for better tree-shaking.**

```typescript
// ✅ Good - const assertions
export const ACCESSION_STATUSES = [
  'Draft',
  'Ready For Testing',
  'Testing',
  'Completed'
] as const;

export type AccessionStatus = typeof ACCESSION_STATUSES[number];

// ✅ Good - readonly configuration objects
export const API_ENDPOINTS = {
  patients: '/v1/patients',
  accessions: '/v1/accessions',
  samples: '/v1/samples'
} as const;

// ❌ Bad - mutable arrays
export const ACCESSION_STATUSES = [
  'Draft',
  'Ready For Testing'
];
```

### 5. Function Return Types

**Always explicitly declare return types for public functions and complex internal functions.**

```typescript
// ✅ Good - explicit return types
export async function createPatient(data: PatientFormData): Promise<PatientDto> {
  const response = await peakLimsApi.post('/v1/patients', data);
  return response.data;
}

export function formatPatientName(patient: PatientDto): string {
  return `${patient.firstName} ${patient.lastName}`;
}

// ✅ Acceptable - simple arrow functions can infer
const isValidPatient = (patient: PatientDto) => patient.id && patient.firstName;
```

## React Component Standards

### 1. Component Definition Pattern

**Use functional components with explicit typing. Use React.forwardRef for components that need ref forwarding.**

```typescript
// ✅ Good - explicit props interface
interface PatientFormProps {
  patient?: PatientDto;
  onSubmit: (data: PatientFormData) => void;
  onCancel?: () => void;
}

export function PatientForm({ patient, onSubmit, onCancel }: PatientFormProps) {
  // Component implementation
}

// ✅ Good - forwardRef when needed
interface ButtonProps extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary';
}

export const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ variant = 'primary', className, ...props }, ref) => {
    return (
      <button
        ref={ref}
        className={cn(buttonVariants({ variant }), className)}
        {...props}
      />
    );
  }
);
Button.displayName = 'Button';
```

### 2. Props Destructuring and Default Values

**Destructure props in function signature. Use default values in destructuring, not within the component.**

```typescript
// ✅ Good - destructure with defaults
interface ComponentProps {
  title: string;
  isVisible?: boolean;
  variant?: 'primary' | 'secondary';
}

export function MyComponent({ 
  title, 
  isVisible = true, 
  variant = 'primary' 
}: ComponentProps) {
  // Implementation
}

// ❌ Bad - default values inside component
export function MyComponent(props: ComponentProps) {
  const isVisible = props.isVisible ?? true;
  const variant = props.variant ?? 'primary';
}
```

### 3. Event Handler Naming

**Use descriptive event handler names. Prefix with `handle` for internal handlers, `on` for prop handlers.**

```typescript
// ✅ Good - descriptive event handler names
interface FormProps {
  onSubmit: (data: FormData) => void;
  onCancel?: () => void;
  onFieldChange?: (field: string, value: unknown) => void;
}

export function PatientForm({ onSubmit, onCancel, onFieldChange }: FormProps) {
  const handleFormSubmit = (data: FormData) => {
    // Validation logic here
    onSubmit(data);
  };

  const handleCancelClick = () => {
    // Cleanup logic here
    onCancel?.();
  };

  const handleFieldUpdate = (field: string, value: unknown) => {
    // Field-specific logic here
    onFieldChange?.(field, value);
  };
}
```

### 4. Conditional Rendering

**Use explicit conditional rendering patterns. Prefer early returns for complex conditions.**

```typescript
// ✅ Good - explicit conditional rendering
export function PatientCard({ patient }: { patient?: PatientDto }) {
  if (!patient) {
    return <div>No patient data available</div>;
  }

  if (patient.status === 'archived') {
    return <ArchivedPatientCard patient={patient} />;
  }

  return (
    <div>
      <h3>{patient.firstName} {patient.lastName}</h3>
      {patient.dateOfBirth && (
        <p>DOB: {formatDate(patient.dateOfBirth)}</p>
      )}
    </div>
  );
}

// ❌ Bad - complex nested ternaries
export function PatientCard({ patient }: { patient?: PatientDto }) {
  return (
    <div>
      {!patient ? (
        <div>No patient data</div>
      ) : patient.status === 'archived' ? (
        <ArchivedPatientCard patient={patient} />
      ) : (
        <div>
          <h3>{patient.firstName} {patient.lastName}</h3>
          {patient.dateOfBirth ? (
            <p>DOB: {formatDate(patient.dateOfBirth)}</p>
          ) : null}
        </div>
      )}
    </div>
  );
}
```

## API and Data Fetching Standards

### 1. TanStack Query Hook Pattern

**Separate API functions from React Query hooks. Use consistent naming patterns.**

```typescript
// ✅ Good - separate API function and hook
// api function
export const getPatient = async (patientId: string): Promise<PatientDto> => {
  const response = await peakLimsApi.get(`/v1/patients/${patientId}`);
  return response.data;
};

// React Query hook
export function useGetPatient(patientId: string | undefined) {
  return useQuery({
    queryKey: PatientKeys.detail(patientId!),
    queryFn: () => getPatient(patientId!),
    enabled: patientId !== null && patientId !== undefined,
  });
}

// ✅ Good - mutation pattern
export const updatePatient = async (
  patientId: string, 
  data: Partial<PatientDto>
): Promise<PatientDto> => {
  const response = await peakLimsApi.put(`/v1/patients/${patientId}`, data);
  return response.data;
};

export function useUpdatePatient(
  options?: UseMutationOptions<PatientDto, AxiosError, { id: string; data: Partial<PatientDto> }>
) {
  const queryClient = useQueryClient();
  
  return useMutation({
    mutationFn: ({ id, data }) => updatePatient(id, data),
    onSuccess: (updatedPatient) => {
      queryClient.invalidateQueries({ queryKey: PatientKeys.lists() });
      queryClient.setQueryData(
        PatientKeys.detail(updatedPatient.id), 
        updatedPatient
      );
    },
    ...options,
  });
}
```

### 2. Query Key Management

**Use query key factories for consistent cache management.**

```typescript
// ✅ Good - query key factory pattern
export const PatientKeys = {
  all: ['patients'] as const,
  lists: () => [...PatientKeys.all, 'list'] as const,
  list: (filters: string) => [...PatientKeys.lists(), { filters }] as const,
  details: () => [...PatientKeys.all, 'detail'] as const,
  detail: (id: string) => [...PatientKeys.details(), id] as const,
  relationships: (patientId: string) => [
    ...PatientKeys.detail(patientId), 
    'relationships'
  ] as const,
};
```

### 3. Error Handling

**Implement consistent error handling patterns with proper typing.**

```typescript
// ✅ Good - typed error handling
interface ApiError {
  message: string;
  status: number;
  code?: string;
}

export async function fetchPatientWithErrorHandling(
  patientId: string
): Promise<PatientDto> {
  try {
    const patient = await getPatient(patientId);
    return patient;
  } catch (error) {
    if (axios.isAxiosError(error)) {
      const apiError: ApiError = {
        message: error.response?.data?.message ?? 'Unknown API error',
        status: error.response?.status ?? 500,
        code: error.response?.data?.code,
      };
      throw apiError;
    }
    throw new Error(`Unexpected error: ${error.message}`);
  }
}

// ✅ Good - error handling in hooks
export function useGetPatientWithErrorHandling(patientId: string) {
  return useQuery({
    queryKey: PatientKeys.detail(patientId),
    queryFn: () => fetchPatientWithErrorHandling(patientId),
    retry: (failureCount, error) => {
      // Don't retry on 404s
      if (error && 'status' in error && error.status === 404) {
        return false;
      }
      return failureCount < 3;
    },
  });
}
```

## State Management Standards

### 1. Zustand Store Pattern

**Use typed Zustand stores with clear action naming. Keep stores focused and cohesive.**

```typescript
// ✅ Good - typed Zustand store
interface PatientFilterStore {
  // State
  searchQuery: string;
  selectedStatuses: string[];
  sortField: keyof PatientDto;
  sortDirection: 'asc' | 'desc';
  
  // Actions
  setSearchQuery: (query: string) => void;
  addStatus: (status: string) => void;
  removeStatus: (status: string) => void;
  clearFilters: () => void;
  setSorting: (field: keyof PatientDto, direction: 'asc' | 'desc') => void;
  
  // Computed
  isFiltered: boolean;
  buildFilterQuery: () => string;
}

export const usePatientFilterStore = create<PatientFilterStore>((set, get) => ({
  // Initial state
  searchQuery: '',
  selectedStatuses: [],
  sortField: 'lastName',
  sortDirection: 'asc',
  
  // Actions
  setSearchQuery: (query) => set({ searchQuery: query }),
  addStatus: (status) => set((state) => ({
    selectedStatuses: [...state.selectedStatuses, status]
  })),
  removeStatus: (status) => set((state) => ({
    selectedStatuses: state.selectedStatuses.filter(s => s !== status)
  })),
  clearFilters: () => set({
    searchQuery: '',
    selectedStatuses: [],
    sortField: 'lastName',
    sortDirection: 'asc'
  }),
  setSorting: (field, direction) => set({
    sortField: field,
    sortDirection: direction
  }),
  
  // Computed values
  get isFiltered() {
    const state = get();
    return state.searchQuery.length > 0 || state.selectedStatuses.length > 0;
  },
  buildFilterQuery: () => {
    const state = get();
    const filters: string[] = [];
    
    if (state.searchQuery) {
      filters.push(`name @=* "${state.searchQuery}"`);
    }
    
    if (state.selectedStatuses.length > 0) {
      const statusFilter = state.selectedStatuses
        .map(status => `status == "${status}"`)
        .join(' || ');
      filters.push(`(${statusFilter})`);
    }
    
    return filters.join(' && ');
  },
}));
```

### 2. Form State with React Hook Form + Zod

**Use Zod schemas for validation with descriptive error messages. Keep form logic clean and reusable.**

```typescript
// ✅ Good - comprehensive Zod schema
export const patientFormSchema = z.object({
  firstName: z
    .string()
    .min(1, 'First name is required')
    .max(50, 'First name must be less than 50 characters')
    .regex(/^[a-zA-Z\s'-]+$/, 'First name contains invalid characters'),
  lastName: z
    .string()
    .min(1, 'Last name is required')
    .max(50, 'Last name must be less than 50 characters'),
  dateOfBirth: z
    .date({
      required_error: 'Date of birth is required',
      invalid_type_error: 'Please enter a valid date',
    })
    .refine((date) => date <= new Date(), {
      message: 'Date of birth cannot be in the future',
    })
    .refine((date) => {
      const age = new Date().getFullYear() - date.getFullYear();
      return age <= 150;
    }, {
      message: 'Please enter a valid date of birth',
    }),
  sex: z.enum(['Male', 'Female', 'Other'], {
    required_error: 'Sex is required',
  }),
  email: z
    .string()
    .email('Please enter a valid email address')
    .optional()
    .or(z.literal('')),
});

export type PatientFormData = z.infer<typeof patientFormSchema>;

// ✅ Good - reusable form hook
export function usePatientForm(
  initialData?: Partial<PatientFormData>,
  onSubmit?: (data: PatientFormData) => void
) {
  const form = useForm<PatientFormData>({
    resolver: zodResolver(patientFormSchema),
    defaultValues: {
      firstName: initialData?.firstName ?? '',
      lastName: initialData?.lastName ?? '',
      sex: initialData?.sex ?? 'Male',
      email: initialData?.email ?? '',
      dateOfBirth: initialData?.dateOfBirth,
    },
  });

  const handleSubmit = form.handleSubmit((data) => {
    onSubmit?.(data);
  });

  return {
    form,
    handleSubmit,
    isValid: form.formState.isValid,
    isDirty: form.formState.isDirty,
    errors: form.formState.errors,
  };
}
```

## Styling and UI Standards

### 1. Tailwind CSS Class Organization

**Order Tailwind classes logically: layout → spacing → sizing → colors → typography → effects.**

```typescript
// ✅ Good - organized class order
<div className="flex flex-col items-center justify-between gap-4 p-6 w-full max-w-md bg-white text-gray-900 text-lg font-medium rounded-lg shadow-lg">

// ❌ Bad - random class order  
<div className="shadow-lg text-lg bg-white rounded-lg font-medium gap-4 flex text-gray-900 max-w-md flex-col p-6 w-full justify-between items-center">
```

### 2. Component Variants with CVA

**Use class-variance-authority for component variants. Keep variants focused and composable.**

```typescript
// ✅ Good - well-structured variants
const buttonVariants = cva(
  // Base styles
  "inline-flex items-center justify-center rounded-md text-sm font-medium transition-colors focus-visible:outline-none focus-visible:ring-2 disabled:opacity-50 disabled:cursor-not-allowed",
  {
    variants: {
      variant: {
        default: "bg-primary text-primary-foreground hover:bg-primary/90",
        destructive: "bg-destructive text-destructive-foreground hover:bg-destructive/90",
        outline: "border border-input bg-background hover:bg-accent hover:text-accent-foreground",
        ghost: "hover:bg-accent hover:text-accent-foreground",
      },
      size: {
        default: "h-9 px-4 py-2",
        sm: "h-8 px-3 text-xs",
        lg: "h-10 px-8",
        icon: "h-9 w-9",
      },
      loading: {
        true: "cursor-not-allowed opacity-70",
        false: "",
      },
    },
    compoundVariants: [
      {
        variant: "destructive",
        loading: true,
        class: "bg-destructive/80",
      },
    ],
    defaultVariants: {
      variant: "default",
      size: "default",
      loading: false,
    },
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
  loading?: boolean;
}
```

## File Organization and Naming

### 1. Import Organization

**Organize imports in groups with blank lines between: external → internal → relative.**

```typescript
// ✅ Good - organized imports
// External libraries
import React from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import * as z from 'zod';

// Internal modules (using @ alias)
import { Button } from '@/components/ui/button';
import { Form, FormControl, FormField } from '@/components/ui/form';
import { peakLimsApi } from '@/services/api-client';
import { useDebounce } from '@/hooks/use-debounce';

// Relative imports
import { PatientDto } from '../types';
import { PatientKeys } from './patient.keys';
```

### 2. Export Patterns

**Use named exports by default. Use default exports only for main component files.**

```typescript
// ✅ Good - named exports for utilities
export const formatPatientName = (patient: PatientDto): string => {
  return `${patient.firstName} ${patient.lastName}`;
};

export const calculateAge = (dateOfBirth: Date): number => {
  return new Date().getFullYear() - dateOfBirth.getFullYear();
};

// ✅ Good - default export for main component
export default function PatientDetailPage() {
  // Component implementation
}
```

### 3. File Naming Conventions

**Use kebab-case for files, PascalCase for components, camelCase for functions and variables.**

```
// ✅ Good file naming
src/
  domain/
    patients/
      components/
        patient-form.tsx          // Component file
        patient-card.tsx          // Component file
        patient-relationship-form.tsx  // Multi-word component
      apis/
        get-patient.tsx           // API function file
        update-patient.tsx        // API function file
        patient.keys.ts           // Query keys file
      types/
        index.ts                  // Type definitions (actual types, not re-exports)
      hooks/
        use-patient-form.tsx      // Custom hook file
```

### 4. No Barrel Files

**Do not use barrel files (index.ts files that only re-export from other modules). Import directly from the source file.**

Barrel files create several problems:
- **Circular dependency risks**: Re-exports can create hard-to-debug circular import issues
- **Bundle size impact**: Bundlers may struggle to tree-shake barrel files effectively
- **Slower IDE performance**: TypeScript language server has to resolve more indirection
- **Ambiguous imports**: Makes it harder to find where code actually lives

```typescript
// ❌ Bad - barrel file (index.ts) with re-exports
// domain/patients/apis/index.ts
export { getPatient, useGetPatient } from './get-patient';
export { updatePatient, useUpdatePatient } from './update-patient';
export { PatientKeys } from './patient.keys';

// ❌ Bad - importing from barrel
import { useGetPatient, PatientKeys } from '@/domain/patients/apis';

// ✅ Good - direct imports from source files
import { useGetPatient } from '@/domain/patients/apis/get-patient';
import { PatientKeys } from '@/domain/patients/apis/patient.keys';
```

**Exception**: Type definition files (`types/index.ts`) may contain actual type definitions in a single file. This is not a barrel file since it contains the actual code, not re-exports.

```typescript
// ✅ Good - types/index.ts contains actual type definitions
// domain/patients/types/index.ts
export interface PatientDto {
  id: string;
  firstName: string;
  lastName: string;
}

export interface PatientFormData {
  firstName: string;
  lastName: string;
}

// ✅ Good - importing types from a type definition file
import type { PatientDto, PatientFormData } from '@/domain/patients/types';
```

## Documentation and Code Comments

### 1. JSDoc for Public APIs

**Use JSDoc comments for exported functions, especially complex ones.**

```typescript
/**
 * Formats a patient's full name with optional title and suffix
 * @param patient - The patient object containing name information
 * @param options - Formatting options
 * @param options.includeTitle - Whether to include professional title
 * @param options.includeMiddleName - Whether to include middle name if available
 * @returns Formatted full name string
 * 
 * @example
 * ```typescript
 * const name = formatPatientName(patient, { includeTitle: true });
 * // Returns: "Dr. John Michael Smith"
 * ```
 */
export function formatPatientName(
  patient: PatientDto,
  options: {
    includeTitle?: boolean;
    includeMiddleName?: boolean;
  } = {}
): string {
  // Implementation
}
```

### 2. Code Comments for Complex Logic

**Add comments for business logic, complex algorithms, or non-obvious code.**

```typescript
// ✅ Good - explaining complex business logic
export function calculatePanelPriority(testOrders: TestOrderDto[]): 'Normal' | 'STAT' {
  // Business rule: If any test in the panel is STAT priority,
  // the entire panel becomes STAT priority
  const hasStatTest = testOrders.some(order => order.priority === 'STAT');
  
  // Additional rule: Emergency department orders are always STAT
  // regardless of individual test priority
  const hasEmergencyOrigin = testOrders.some(order => 
    order.originDepartment?.includes('Emergency')
  );
  
  return hasStatTest || hasEmergencyOrigin ? 'STAT' : 'Normal';
}

// ✅ Good - explaining technical implementation
export function useDebouncedSearch<T>(
  searchFn: (query: string) => Promise<T[]>,
  delay: number = 300
) {
  // Use separate state for the debounced value to prevent
  // unnecessary re-renders during typing
  const [query, setQuery] = useState('');
  const debouncedQuery = useDebounce(query, delay);
  
  // Only trigger search when debounced value changes,
  // not on every keystroke
  const searchResults = useQuery({
    queryKey: ['search', debouncedQuery],
    queryFn: () => searchFn(debouncedQuery),
    enabled: debouncedQuery.length > 2, // Minimum 3 characters
  });

  return { query, setQuery, searchResults };
}
```

## Performance and Best Practices

### 1. React Performance Optimization

**Use React.memo, useMemo, and useCallback judiciously for expensive operations.**

```typescript
// ✅ Good - memo for expensive list items
interface PatientCardProps {
  patient: PatientDto;
  onSelect: (patientId: string) => void;
}

export const PatientCard = React.memo<PatientCardProps>(({ patient, onSelect }) => {
  const handleClick = useCallback(() => {
    onSelect(patient.id);
  }, [patient.id, onSelect]);

  // Expensive calculation that should be memoized
  const patientSummary = useMemo(() => {
    return generatePatientSummary(patient);
  }, [patient]);

  return (
    <div onClick={handleClick}>
      <h3>{patient.firstName} {patient.lastName}</h3>
      <p>{patientSummary}</p>
    </div>
  );
});

PatientCard.displayName = 'PatientCard';
```

### 2. Bundle Optimization

**Use dynamic imports for code splitting on route boundaries and heavy dependencies.**

```typescript
// ✅ Good - lazy loading heavy components
const PatientReportViewer = lazy(() => import('./patient-report-viewer'));
const PatientChartPrinter = lazy(() => import('./patient-chart-printer'));

export function PatientDetail({ patientId }: { patientId: string }) {
  const [showReport, setShowReport] = useState(false);

  return (
    <div>
      <PatientInfo patientId={patientId} />
      
      {showReport && (
        <Suspense fallback={<div>Loading report...</div>}>
          <PatientReportViewer patientId={patientId} />
        </Suspense>
      )}
    </div>
  );
}

// ✅ Good - dynamic imports for heavy utilities
const loadPdfGenerator = () => import('@/utils/pdf-generator');

export async function generatePatientReport(patient: PatientDto) {
  const { generatePdf } = await loadPdfGenerator();
  return generatePdf(patient);
}
```

These standards ensure consistent, maintainable, and performant code across the PeakLIMS React application. All team members should follow these guidelines to maintain code quality and developer productivity.