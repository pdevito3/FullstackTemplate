import { userDtoRolesEnum, type UserDtoRolesEnumKey } from '@/api/generated/models/types/user-dto'

export type UserRole = UserDtoRolesEnumKey

export const USER_ROLES = Object.values(userDtoRolesEnum)

export const ROLE_LABELS: Record<UserRole, string> = {
  Admin: 'Admin',
  User: 'User',
}

export const isUserRole = (value: unknown): value is UserRole => USER_ROLES.some((role) => role === value)

/** The labels of the roles, in the order of the contract, or a dash for a user with no role. */
export const roleLabels = (roles: readonly UserRole[]) =>
  USER_ROLES.filter((role) => roles.includes(role)).map((role) => ROLE_LABELS[role]).join(', ') || '—'
