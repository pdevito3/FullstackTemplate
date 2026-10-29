export const UserKeys = {
  all: ['users'] as const,
  current: () => [...UserKeys.all, 'current'] as const,
}
