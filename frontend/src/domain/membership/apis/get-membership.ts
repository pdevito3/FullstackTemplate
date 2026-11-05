import { useState } from 'react'
import { queryOptions, useQuery } from '@tanstack/react-query'

import { getUserByIdentifier } from '@/api/generated/users/clients/get-user-by-identifier'
import type { InitiateUserAndTenantDto } from '@/api/generated/models/types/initiate-user-and-tenant-dto'
import type { UserDto } from '@/api/generated/models/types/user-dto'
import { authClaimsQuery } from '@/domain/auth/apis/get-user'
import type { BffClaim } from '@/domain/auth/types'
import { isProblemDetails, isProblemDetailsError, ProblemDetailsError } from '@/lib/problem-details'

import { MembershipKeys } from './membership.keys'

/** The fields of a new tenant and its first user that the identity provider can fill in. */
export type StartTenantProfile = Pick<InitiateUserAndTenantDto, 'identifier' | 'firstName' | 'lastName' | 'email' | 'username'>

/** The state of the signed-in person's user record on the Server. A user record means a tenant. */
export type Membership =
  | { status: 'loading' }
  | { status: 'error'; message: string; isRetrying?: boolean; retry?: () => void }
  | { status: 'member'; user: UserDto }
  | { status: 'no-tenant'; profile: StartTenantProfile }

/** The `sub` of the signed-in person, once the membership is known. */
export function subOf(membership: Membership): string | undefined {
  if (membership.status === 'member') return membership.user.identifier
  return membership.status === 'no-tenant' ? membership.profile.identifier : undefined
}

const claim = (claims: BffClaim[], type: string) => claims.find((c) => c.type === type)?.value

export const subOfClaims = (claims: BffClaim[] | null | undefined) => (claims ? claim(claims, 'sub') : undefined)

function profileOf(identifier: string, claims: BffClaim[]): StartTenantProfile {
  const email = claim(claims, 'email') ?? ''
  return {
    identifier,
    firstName: claim(claims, 'given_name') ?? '',
    lastName: claim(claims, 'family_name') ?? '',
    email,
    username: claim(claims, 'preferred_username') ?? email,
  }
}

async function findUser(identifier: string, signal: AbortSignal): Promise<UserDto | null> {
  // The pages show the failure, so a toast would say it twice.
  const result = await getUserByIdentifier({
    path: { identifier },
    throwOnError: false,
    signal,
    options: { suppressErrorToast: true },
  })
  if (result.status === 200) return result.data
  if (result.status === 404) return null
  throw isProblemDetails(result.error)
    ? new ProblemDetailsError(result.error)
    : new Error(`The membership check failed with status ${result.status}.`)
}

/** A refusal of the Server does not change on an immediate retry. */
export const isRefusal = (error: unknown): error is ProblemDetailsError =>
  isProblemDetailsError(error) && (error.status ?? 500) < 500

const CHECK_FAILED = 'We could not check whether you are a member of a tenant.'

/** The user record of a `sub`, or null when it has none. */
export const callerRecordQuery = (identifier: string) =>
  queryOptions({
    queryKey: MembershipKeys.of(identifier),
    queryFn: ({ signal }) => findUser(identifier, signal),
    retry: (failureCount, error) => !isRefusal(error) && failureCount < 1,
  })

export function useMembership(): Membership {
  const claims = useQuery(authClaimsQuery)
  const identifier = subOfClaims(claims.data)
  const user = useQuery({ ...callerRecordQuery(identifier ?? ''), enabled: !!identifier })
  // A refetch with no data sets the query back to pending and clears the error. The failure must stay on the page until
  // the answer comes, or the focused Try Again goes away.
  const [lastError, setLastError] = useState<unknown>(null)
  if (user.error && user.error !== lastError) setLastError(user.error)

  // A failed refetch keeps the data of the last answer, and that answer is still good to show.
  if (claims.data === undefined) {
    return claims.isError
      ? { status: 'error', message: CHECK_FAILED, retry: () => void claims.refetch() }
      : { status: 'loading' }
  }
  if (!claims.data || !identifier) return { status: 'error', message: CHECK_FAILED }
  if (user.data === undefined) {
    if (user.errorUpdateCount === 0) return { status: 'loading' }
    return {
      status: 'error',
      message: isRefusal(lastError) ? lastError.displayMessage : CHECK_FAILED,
      isRetrying: user.isFetching,
      retry: () => void user.refetch(),
    }
  }
  return user.data
    ? { status: 'member', user: user.data }
    : { status: 'no-tenant', profile: profileOf(identifier, claims.data) }
}
