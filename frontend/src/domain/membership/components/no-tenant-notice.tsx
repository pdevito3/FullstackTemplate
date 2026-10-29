import { useRouterState } from '@tanstack/react-router'

import { Button } from '@/components/ui/button'
import { buttonVariants } from '@/components/ui/button-variants'
import { Link } from '@/components/ui/link'

import { useMembership } from '../apis/get-membership'

/** Tells a signed-in user with no user record, such as each user of a new database, how to start a tenant. */
export function NoTenantNotice() {
  const membership = useMembership()
  const pathname = useRouterState({ select: (state) => state.location.pathname })

  if (pathname === '/start-tenant') return null

  if (membership.status === 'error') {
    return (
      <section
        aria-label="Membership check"
        className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-destructive/20 bg-destructive/10 px-4 py-3 text-sm text-destructive"
      >
        <p className="min-w-0">{membership.message}</p>
        {membership.retry && <Button variant="outline" size="sm" onPress={membership.retry}>Try Again</Button>}
      </section>
    )
  }

  if (membership.status !== 'no-tenant') return null

  return (
    <section
      aria-label="No tenant"
      className="flex flex-wrap items-center justify-between gap-3 rounded-lg border border-border bg-muted/50 px-4 py-3 text-sm"
    >
      <p className="min-w-0">
        You are not a member of a tenant. Start a tenant to become its admin, or ask the admin of a tenant to add you.
      </p>
      <Link to="/start-tenant" className={buttonVariants({ size: 'sm' })}>
        Start a tenant
      </Link>
    </section>
  )
}
