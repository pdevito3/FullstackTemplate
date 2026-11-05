import { useEffect, useRef } from 'react'
import { useRouterState } from '@tanstack/react-router'

import { Button } from '@/components/ui/button'
import { buttonVariants } from '@/components/ui/button-variants'
import { Link } from '@/components/ui/link'
import { focusWithoutTabStop } from '@/lib/focus-without-tab-stop'

import { useMembership } from '../apis/get-membership'

// Each of these pages tells the user what to do next, and on /join the user is about to join a tenant.
const PAGES_WITH_NO_NOTICE = ['/start-tenant', '/join']

/** Tells a signed-in user with no user record, such as each user of a new database, how to start a tenant. */
export function NoTenantNotice() {
  const membership = useMembership()
  const pathname = useRouterState({ select: (state) => state.location.pathname })
  const noticeLink = useRef<HTMLAnchorElement>(null)
  const pressedTryAgain = useRef(false)

  // Try Again goes away with the answer, so the focus moves to the link of the notice or the page heading.
  useEffect(() => {
    if (!pressedTryAgain.current || membership.status === 'error') return
    pressedTryAgain.current = false
    if (noticeLink.current) noticeLink.current.focus()
    else focusWithoutTabStop(document.querySelector<HTMLElement>('main h1'))
  }, [membership.status])

  if (PAGES_WITH_NO_NOTICE.includes(pathname.replace(/\/$/, ''))) return null

  if (membership.status === 'error') {
    return (
      <section
        aria-label="Membership check"
        className="mb-4 flex flex-wrap items-center justify-between gap-3 border-y border-destructive/20 bg-destructive/10 px-4 py-3 text-sm text-destructive"
      >
        <p className="min-w-0">{membership.message}</p>
        {membership.retry && (
          <Button
            variant="outline"
            size="sm"
            isPending={membership.isRetrying}
            onPress={() => {
              pressedTryAgain.current = true
              membership.retry?.()
            }}
          >
            Try Again
          </Button>
        )}
      </section>
    )
  }

  if (membership.status !== 'no-tenant') return null

  return (
    <section
      aria-label="No tenant"
      className="mb-4 flex flex-wrap items-center justify-between gap-3 border-y border-border bg-muted/50 px-4 py-3 text-sm"
    >
      <p className="min-w-0">
        You are not a member of a tenant. Start a tenant to become its admin, or ask the admin of a tenant to add you.
      </p>
      <Link ref={noticeLink} to="/start-tenant" className={buttonVariants({ size: 'sm' })}>
        Start a tenant
      </Link>
    </section>
  )
}
