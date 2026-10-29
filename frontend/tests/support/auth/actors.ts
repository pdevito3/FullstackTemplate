import type { BffClaim } from '@/domain/auth/types'

// The SPA knows the user only from GET /bff/user. The api fixture mocks that endpoint with the claims of the actor,
// so a test never drives a login form. It also answers the lookup of the user record of the actor on the Server.
export type Identity = { signedIn: false } | { signedIn: true; claims: BffClaim[]; hasTenant: boolean }

export const actorSubject = '1'

const claimsOf = (name: string): BffClaim[] => [
  { type: 'sub', value: actorSubject },
  { type: 'name', value: name },
  { type: 'bff:logout_url', value: '/bff/logout?sid=test-session' },
]

export const as = {
  anonymous: (): Identity => ({ signedIn: false }),
  /** A user with a user record, and so a tenant, on the Server. */
  user: (name = 'Bob Smith'): Identity => ({ signedIn: true, claims: claimsOf(name), hasTenant: true }),
  /** A user that the identity provider knows and the Server does not, as each user of a new database. */
  userWithNoTenant: (name = 'Ada Lovelace'): Identity => {
    const [givenName, familyName] = name.split(' ')
    return {
      signedIn: true,
      hasTenant: false,
      claims: [
        ...claimsOf(name),
        { type: 'given_name', value: givenName },
        { type: 'family_name', value: familyName },
        { type: 'email', value: 'ada@example.com' },
        { type: 'preferred_username', value: 'ada' },
      ],
    }
  },
}
