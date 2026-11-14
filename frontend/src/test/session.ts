import type { CurrentUserDto } from '@/api/generated/models/types/current-user-dto'
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

export const memberClaims: BffClaim[] = [{ type: 'sub', value: member.identifier }]

/** The member as the Server sees the token. The sidebar reads it to show the link to the jobs dashboard. */
export const currentMember: CurrentUserDto = { identifier: member.identifier, name: member.fullName, canOpenJobDashboard: false }

export const signedInMember: Answer[] = [
  { method: 'GET', path: '/bff/user', body: memberClaims },
  { method: 'GET', path: `/api/v1/users/by-identifier/${member.identifier}`, body: member },
  { method: 'GET', path: '/api/v1/users/me', body: currentMember },
]
