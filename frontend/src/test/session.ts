import type { UserDto } from '@/api/generated/models/types/user-dto'
import type { BffClaim } from '@/domain/auth/types'

import type { Answer } from './answer'

export const member: UserDto = {
  id: 'usr_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f',
  tenantId: 'ten_0199f0c1a2b37c4d8e9f0a1b2c3d4e5f',
  firstName: 'Bob',
  lastName: 'Smith',
  fullName: 'Bob Smith',
  identifier: 'bob',
  email: 'bob@example.com',
  username: 'bob',
  role: 'User',
  permissions: [],
  allowedActions: [],
  version: 1,
}

/** The claims of the BFF session of `member`. */
export const memberClaims: BffClaim[] = [{ type: 'sub', value: member.identifier }]

/** The answers for a signed-in member of a tenant: the session of the BFF, and the user record that the layout looks up. */
export const signedInMember: Answer[] = [
  { method: 'GET', path: '/bff/user', body: memberClaims },
  { method: 'GET', path: `/api/v1/users/by-identifier/${member.identifier}`, body: member },
]
