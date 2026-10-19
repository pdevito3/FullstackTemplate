# Frontend code style

These rules apply to the TypeScript and React code in `frontend/src/`. `pnpm lint` and `tsc` (in `pnpm build`) enforce part of them.

## TypeScript

1. Do not use `any`. Use `unknown` and a type guard.
2. Use the generated types for API data, for example `UserDto` from `@/api/generated/models/types/user-dto`. Do not write a DTO type by hand.
3. Use `interface` for an object shape. Use `type` for a union, a function type, or a computed type.
4. Use a string union, not an `enum`. Make a list of options with `as const`.
5. Give an explicit return type to each exported function, except a hook or a short arrow function.
6. Use `async` and `await`, not `.then` chains.
7. Use `import type` for an import that is only a type.

```typescript
export const USER_ROLES = ['Admin', 'User'] as const
export type UserRole = (typeof USER_ROLES)[number]

function isUserDto(value: unknown): value is UserDto {
  return typeof value === 'object' && value !== null && 'id' in value
}
```

## React components

1. Write function components. Declare a `Props` interface.
2. Destructure the props in the signature, and put the default values there.
3. React 19 passes `ref` as a prop. Do not use `forwardRef`.
4. Name a handler prop `onX`. Name the handler inside the component `handleX`.
5. Use an early return for a loading, empty, or error state. Do not nest ternaries.
6. Load a heavy component or library with `lazy` or a dynamic `import()`.

```typescript
interface UserCardProps {
  user?: UserDto
  isCompact?: boolean
  onSelect: (userId: string) => void
}

export function UserCard({ user, isCompact = false, onSelect }: UserCardProps) {
  if (!user) {
    return <p>No user found.</p>
  }

  const handleClick = () => onSelect(user.id)

  return (
    <button onClick={handleClick}>
      {user.fullName}
      {!isCompact && <span>{user.email}</span>}
    </button>
  )
}
```

## Styles

1. Use Tailwind classes. Merge classes with `cn` from `@/lib/utils`.
2. Make component variants with `cva`. See `components/ui/button.tsx`.
3. Use the theme tokens, such as `bg-background` and `text-muted-foreground`. Do not use a fixed color, because the dark theme does not change it.

## Files and imports

1. Name files in kebab case, for example `user-card.tsx`. Name components in PascalCase, and functions and variables in camelCase.
2. Use named exports. A route file exports `Route`.
3. Do not make barrel files. A barrel file is an `index.ts` that only exports from other files. Import from the file that has the code. `types/index.ts` is correct, because it holds the types.
4. Use the `@/` alias for imports outside the current area. Use a relative import inside the area.
5. Order imports in this sequence: packages, `@/` imports, and relative imports.

```typescript
import { useQuery } from '@tanstack/react-query'

import { getWeatherForecast } from '@/api/generated/weather/clients/get-weather-forecast'

import { WeatherKeys } from './weather.keys'
```

## Comments

Give a JSDoc comment to each exported function that is not obvious. Add a comment where the code follows a business rule or does a thing that a reader does not expect. Do not describe what the code already says.
