import type { UserListParams } from './get-user-list'

export const UserKeys = {
  all: ['users'] as const,
  current: () => [...UserKeys.all, 'current'] as const,
  lists: () => [...UserKeys.all, 'list'] as const,
  list: (params: UserListParams) => [...UserKeys.lists(), params] as const,
  details: () => [...UserKeys.all, 'detail'] as const,
  detail: (id: string) => [...UserKeys.details(), id] as const,
}
