import { userDtoRoleEnum, type UserDtoRoleEnumKey } from '@/api/generated/models/types/user-dto'

export type UserRole = UserDtoRoleEnumKey

export const USER_ROLES = Object.values(userDtoRoleEnum)

export const ROLE_LABELS: Record<UserRole, string> = {
  Admin: 'Admin',
  User: 'User',
}

export const isUserRole = (value: unknown): value is UserRole => USER_ROLES.some((role) => role === value)
