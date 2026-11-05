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
  // The page on the screen. The location changes first, and the old page stays until the code of the next page loads.
  const pathname = useRouterState({ select: (state) => state.matches[state.matches.length - 1]?.pathname ?? state.location.pathname })
  const noticeLink = useRef<HTMLAnchorElement>(null)
  const isFailed = membership.status === 'error'
  const wasFailed = useRef(false)

  // Try Again goes away when the check stops failing, on this banner or on a page, and its focus falls to the body.
  // Focus on another element is where the user went while the answer was on the way, so it stays there.
  useEffect(() => {
    const stoppedFailing = wasFailed.current && !isFailed
    wasFailed.current = isFailed
    if (!stoppedFailing || document.activeElement !== document.body) return
    if (noticeLink.current) noticeLink.current.focus({ preventScroll: true })
    else focusWithoutTabStop(document.querySelector<HTMLElement>('main h1'))
  }, [isFailed])

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
            onPress={membership.retry}
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
