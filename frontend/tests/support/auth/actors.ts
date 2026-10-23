import type { BffClaim } from '@/domain/auth/types'

// The SPA knows the user only from GET /bff/user. The api fixture mocks that endpoint with the claims of the actor,
// so a test never drives a login form.
export type Identity = { signedIn: false } | { signedIn: true; claims: BffClaim[] }

export const as = {
  anonymous: (): Identity => ({ signedIn: false }),
  user: (name = 'Bob Smith'): Identity => ({
    signedIn: true,
    claims: [
      { type: 'sub', value: '1' },
      { type: 'name', value: name },
      { type: 'bff:logout_url', value: '/bff/logout?sid=test-session' },
    ],
  }),
}
