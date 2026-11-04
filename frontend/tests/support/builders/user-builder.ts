import type { InitiateUserAndTenantResponseDto } from '@/api/generated/models/types/initiate-user-and-tenant-response-dto'
import type {
  UserDto,
  UserDtoAllowedActionsEnumKey,
  UserDtoRoleEnumKey,
} from '@/api/generated/models/types/user-dto'

let seed = 0
const next = () => ++seed

const ADMIN_ACTIONS: UserDtoAllowedActionsEnumKey[] = ['Update', 'UpdateRole', 'AddPermission', 'RemovePermission', 'Delete']

class UserBuilder {
  private readonly user: UserDto

  private constructor(user: UserDto) {
    this.user = user
  }

  static create() {
    return new UserBuilder({
      id: `usr_${next()}`,
      tenantId: 'tnt_1',
      firstName: 'Bob',
      lastName: 'Smith',
      fullName: 'Bob Smith',
      identifier: '1',
      email: 'bob@example.com',
      username: 'bob',
      role: 'User',
      permissions: [],
      allowedActions: [],
      version: 1,
    })
  }

  private with(patch: Partial<UserDto>) {
    return new UserBuilder({ ...this.user, ...patch })
  }

  /** The `sub` claim of the user. */
  identifiedBy(identifier: string) {
    return this.with({ identifier })
  }

  named(fullName: string) {
    const [firstName, ...rest] = fullName.split(' ')
    const username = firstName.toLowerCase()
    return this.with({ firstName, lastName: rest.join(' '), fullName, username, email: `${username}@example.com` })
  }

  inRole(role: UserDtoRoleEnumKey) {
    return this.with({ role })
  }

  seenByAdmin() {
    return this.with({ allowedActions: ADMIN_ACTIONS })
  }

  allowing(...allowedActions: UserDtoAllowedActionsEnumKey[]) {
    return this.with({ allowedActions })
  }

  savedAgain() {
    return this.with({ version: this.user.version + 1 })
  }

  build(): UserDto {
    return this.user
  }
}

export const aUser = () => UserBuilder.create()

export const aStartedTenant = (name: string, user = aUser()): InitiateUserAndTenantResponseDto => {
  const admin = { ...user.build(), role: 'Admin' as const }
  return { tenant: { id: admin.tenantId, name, allowedActions: ['InviteUser'], version: 1 }, user: admin }
}
