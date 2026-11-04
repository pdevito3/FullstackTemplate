import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useRef, useState } from 'react'
import type { RefObject } from 'react'

import { inviteTokenDtoSchema } from '@/api/generated/models/zod/invite-token-dto'
import { Notification } from '@/components/notification'
import { PageHeading } from '@/components/page-heading'
import { QueryError } from '@/components/query-error'
import { Button } from '@/components/ui/button'
import { buttonVariants } from '@/components/ui/button-variants'
import { Link } from '@/components/ui/link'
import { Skeleton } from '@/components/ui/skeleton'
import { useClaimInvite } from '@/domain/invites/apis/claim-invite'
import type { ClaimValues } from '@/domain/invites/apis/claim-invite'
import { useInvitePreview } from '@/domain/invites/apis/get-invite-preview'
import { ClaimInviteForm } from '@/domain/invites/components/claim-invite-form'
import { formatExpiry } from '@/domain/invites/components/invite-state'
import { inviteTokenFor } from '@/domain/invites/invite-link'
import { ALREADY_MEMBER, joinViewOf } from '@/domain/invites/join-view'
import type { JoinView, TabLink } from '@/domain/invites/join-view'
import { subOf, useMembership } from '@/domain/membership/apis/get-membership'
import type { Membership } from '@/domain/membership/apis/get-membership'
import { ROLE_LABELS } from '@/domain/users/components/user-role'
import { maxLengthOf } from '@/lib/api-contract'
import { isProblemDetailsError } from '@/lib/problem-details'

export const Route = createFileRoute('/join')({
  component: JoinPage,
  staticData: { breadcrumb: 'Join a tenant' },
})

const tokenLimit = maxLengthOf(inviteTokenDtoSchema.shape.token)

function linkOfTheTab(sub: string | undefined): TabLink {
  const token = sub ? inviteTokenFor(sub) : null
  if (!token) return 'none'
  return token.length <= tokenLimit ? 'fits' : 'too-long'
}

function JoinPage() {
  const headingRef = useRef<HTMLHeadingElement>(null)
  const membership = useMembership()

  return (
    <div className="mx-auto w-full max-w-4xl px-4 py-8">
      <PageHeading ref={headingRef}>Join a tenant</PageHeading>
      <div className="mt-6">
        {/* Each new `sub` reads the tab again, and the link of another person goes. */}
        <Join key={subOf(membership)} membership={membership} headingRef={headingRef} />
      </div>
    </div>
  )
}

function Join({ membership, headingRef }: { membership: Membership; headingRef: RefObject<HTMLHeadingElement | null> }) {
  const [link] = useState(() => linkOfTheTab(subOf(membership)))
  const [refusedLink, setRefusedLink] = useState<string | null>(null)
  const preview = useInvitePreview({ enabled: membership.status === 'no-tenant' && link === 'fits' && !refusedLink })
  const claim = useClaimInvite()
  const navigate = useNavigate()
  const view = joinViewOf({ membership, link, preview, refusedLink, claim: claim.status })

  const handleClaim = async (values: ClaimValues, identifier: string, tenantName: string) => {
    try {
      await claim.mutateAsync({ values, identifier, tenantName })
    } catch (error) {
      const status = isProblemDetailsError(error) ? error.status : undefined
      if (isProblemDetailsError(error) && (status === 404 || status === 410)) setRefusedLink(error.displayMessage)
      // These refusals replace the form, and the focus must not fall to the body.
      if (status === 404 || status === 409 || status === 410) headingRef.current?.focus()
      throw error
    }
    Notification.success(`You joined ${tenantName}.`)
    await navigate({ to: '/' })
  }

  return <JoinContent view={view} onRetryPreview={() => void preview.refetch()} onClaim={handleClaim} />
}

interface JoinContentProps {
  view: JoinView
  onRetryPreview: () => void
  onClaim: (values: ClaimValues, identifier: string, tenantName: string) => Promise<void>
}

function JoinContent({ view, onRetryPreview, onClaim }: JoinContentProps) {
  switch (view.kind) {
    case 'loading':
      return <Skeleton role="status" aria-label="Loading the invite" className="h-64 max-w-lg" />
    case 'joining':
      return <p role="status" className="text-muted-foreground">Joining the tenant</p>
    case 'membership-failed':
      return (
        <div role="alert" className="flex flex-col items-start gap-3 rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-destructive wrap-anywhere">
          <p>{view.message}</p>
          {view.retry && <Button variant="outline" onPress={view.retry}>Try again</Button>}
        </div>
      )
    case 'member':
      return (
        <div className="flex flex-col items-start gap-4">
          <p className="text-muted-foreground">{ALREADY_MEMBER}</p>
          <Link to="/" className={buttonVariants({ variant: 'outline' })}>Go to the home page</Link>
        </div>
      )
    case 'link-refused':
      return (
        <div className="flex flex-col items-start gap-4">
          <p role="alert" className="w-full rounded-lg border border-destructive/20 bg-destructive/10 p-4 text-destructive wrap-anywhere">
            {view.message}
          </p>
          <Link to="/" className={buttonVariants({ variant: 'outline' })}>Go to the home page</Link>
        </div>
      )
    case 'preview-failed':
      return <QueryError title="The invite did not load." error={view.error} onRetry={onRetryPreview} />
    case 'claimable': {
      const { preview, profile } = view
      return (
        <div className="flex flex-col gap-6">
          <section aria-label="Invite">
            <dl className="grid max-w-lg grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm wrap-anywhere">
              <dt className="text-muted-foreground">Tenant</dt>
              <dd className="min-w-0">{preview.tenantName}</dd>
              <dt className="text-muted-foreground">Role</dt>
              <dd>{ROLE_LABELS[preview.role]}</dd>
              <dt className="text-muted-foreground">Expires</dt>
              <dd>{formatExpiry(preview.expiresAt)}</dd>
            </dl>
          </section>
          <ClaimInviteForm
            profile={profile}
            requireEmailMatch={preview.requireEmailMatch}
            onClaim={(values) => onClaim(values, profile.identifier, preview.tenantName)}
          />
        </div>
      )
    }
  }
}
