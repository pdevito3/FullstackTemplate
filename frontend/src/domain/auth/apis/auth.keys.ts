/**
 * Query Key Factory for Auth domain
 *
 * Provides type-safe, hierarchical query keys for TanStack Query.
 */
export const AuthKeys = {
  all: ['auth'] as const,
  user: () => [...AuthKeys.all, 'user'] as const,
}
