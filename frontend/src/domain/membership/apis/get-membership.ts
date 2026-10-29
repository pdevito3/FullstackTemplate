import { useQuery } from '@tanstack/react-query'

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
  | { status: 'signed-out' }
  | { status: 'error'; message: string; retry?: () => void }
  | { status: 'member'; user: UserDto }
  | { status: 'no-tenant'; profile: StartTenantProfile }

const claim = (claims: BffClaim[], type: string) => claims.find((c) => c.type === type)?.value

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

/** A refusal of the Server, such as a 403 for a deleted user whose tenant is still cached, does not change on an immediate retry. */
const isRefusal = (error: unknown): error is ProblemDetailsError =>
  isProblemDetailsError(error) && (error.status ?? 500) < 500

const CHECK_FAILED = 'We could not check whether you are a member of a tenant.'

export function useMembership(): Membership {
  const claims = useQuery(authClaimsQuery)
  const identifier = claims.data ? claim(claims.data, 'sub') : undefined
  const user = useQuery({
    queryKey: MembershipKeys.of(identifier ?? ''),
    queryFn: ({ signal }) => findUser(identifier!, signal),
    enabled: !!identifier,
    retry: (failureCount, error) => !isRefusal(error) && failureCount < 1,
  })

  // A failed refetch keeps the data of the last answer, and that answer is still good to show.
  if (claims.data === undefined) {
    return claims.isError
      ? { status: 'error', message: CHECK_FAILED, retry: () => void claims.refetch() }
      : { status: 'loading' }
  }
  if (!claims.data || !identifier) return { status: 'signed-out' }
  if (user.data === undefined) {
    if (!user.isError) return { status: 'loading' }
    return isRefusal(user.error)
      ? { status: 'error', message: user.error.displayMessage }
      : { status: 'error', message: CHECK_FAILED, retry: () => void user.refetch() }
  }
  return user.data
    ? { status: 'member', user: user.data }
    : { status: 'no-tenant', profile: profileOf(identifier, claims.data) }
}
