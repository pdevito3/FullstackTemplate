import type { InitiateUserAndTenantResponseDto } from '@/api/generated/models/types/initiate-user-and-tenant-response-dto'
import type { UserDto } from '@/api/generated/models/types/user-dto'

class UserBuilder {
  private readonly user: UserDto

  private constructor(user: UserDto) {
    this.user = user
  }

  static create() {
    return new UserBuilder({
      id: 'usr_1',
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

  /** The `sub` claim of the user. */
  identifiedBy(identifier: string) {
    return new UserBuilder({ ...this.user, identifier })
  }

  build(): UserDto {
    return this.user
  }
}

export const aUser = () => UserBuilder.create()

export const aStartedTenant = (name: string, user = aUser()): InitiateUserAndTenantResponseDto => {
  const admin = { ...user.build(), role: 'Admin' as const }
  return { tenant: { id: admin.tenantId, name, version: 1 }, user: admin }
}
