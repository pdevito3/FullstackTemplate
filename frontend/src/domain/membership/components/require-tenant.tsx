import type { ReactNode } from 'react'

import { Link } from '@/components/ui/link'
import { Skeleton } from '@/components/ui/skeleton'

import { useMembership } from '../apis/get-membership'

/** The way to start a tenant, in place of a part of a page that needs one. `purpose` ends the sentence: "to add rooms." */
export function StartTenantPrompt({ purpose }: { purpose: string }) {
  return (
    <p className="text-muted-foreground">
      <Link to="/start-tenant" className="underline underline-offset-4 hover:text-primary">
        Start a tenant
      </Link>{' '}
      {purpose}
    </p>
  )
}

interface RequireTenantProps {
  purpose: string
  children: ReactNode
}

/**
 * Shows a form that saves in a tenant. A user with no tenant gets the way to start one, because the Server refuses each
 * save of that user. A failed membership check keeps the form, and the notice of the layout shows the failure.
 */
export function RequireTenant({ purpose, children }: RequireTenantProps) {
  const { status } = useMembership()
  if (status === 'loading') return <Skeleton role="status" aria-label="Loading your account" className="h-64 max-w-lg" />
  if (status === 'no-tenant') return <StartTenantPrompt purpose={purpose} />
  return children
}
