import { useRef } from 'react'

import type { InviteDto } from '@/api/generated/models/types/invite-dto'
import { Notification } from '@/components/notification'
import { QueryError } from '@/components/query-error'
import { Badge } from '@/components/ui/badge'
import { Link } from '@/components/ui/link'
import { Pager } from '@/components/ui/pager'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { ROLE_LABELS } from '@/domain/users/components/user-role'
import { useIsMobile } from '@/hooks/use-mobile'

import { useInviteList } from '../apis/get-invite-list'
import { useRevokeInvite } from '../apis/revoke-invite'
import { InviteActions } from './invite-actions'
import { formatExpiry, INVITE_STATE_LABELS, inviteStateOf, type InviteState } from './invite-state'

const STATE_VARIANTS: Record<InviteState, 'secondary' | 'default' | 'destructive' | 'outline'> = {
  Pending: 'default',
  Claimed: 'secondary',
  Revoked: 'destructive',
  Expired: 'outline',
}

interface InviteListProps {
  page: number
  onPageChange: (pageNumber: number) => void
}

export function InviteList({ page, onPageChange }: InviteListProps) {
  const headingRef = useRef<HTMLHeadingElement>(null)

  return (
    <section aria-labelledby="invites-heading" className="flex flex-col gap-4">
      <h2 ref={headingRef} id="invites-heading" tabIndex={-1} className="text-xl font-semibold tracking-tight outline-none">
        Invites
      </h2>
      <InviteRows page={page} onPageChange={onPageChange} onRevokeSettled={() => headingRef.current?.focus()} />
    </section>
  )
}

function InviteRows({ page, onPageChange, onRevokeSettled }: InviteListProps & { onRevokeSettled: () => void }) {
  const list = useInviteList({ page })
  const revoke = useRevokeInvite()
  const isMobile = useIsMobile()

  if (list.isPending) {
    return <p role="status" aria-label="Loading invites" className="text-muted-foreground">Loading invites</p>
  }

  if (list.isError) {
    return <QueryError title="The invites did not load." error={list.error} onRetry={() => list.refetch()} />
  }

  const { invites, pagination } = list.data

  if (invites.length === 0 && page > 1) {
    return (
      <p className="text-muted-foreground">
        There are no invites on page {page}.{' '}
        <Link to="/users" search={(prev) => ({ ...prev, invitesPage: undefined })} className="underline underline-offset-4 hover:text-primary">
          Go to page 1
        </Link>
      </p>
    )
  }

  if (invites.length === 0) {
    return <p className="text-muted-foreground">There are no invites. Choose Invite a user to make a link.</p>
  }

  const handleRevoke = (invite: InviteDto) =>
    revoke.mutate(invite.id, {
      onSuccess: () => Notification.success('Invite revoked'),
      onSettled: onRevokeSettled,
    })

  return (
    <div className="flex flex-col gap-4">
      <div className="rounded-md border">
        <Table aria-label="Invites">
          <TableHeader>
            <TableHead isRowHeader>Email</TableHead>
            {!isMobile && <TableHead>Role</TableHead>}
            <TableHead>State</TableHead>
            {!isMobile && <TableHead>Expires</TableHead>}
            <TableHead><span className="sr-only">Actions</span></TableHead>
          </TableHeader>
          <TableBody>
            {invites.map((invite) => {
              const state = inviteStateOf(invite)
              return (
                <TableRow key={invite.id} id={invite.id}>
                  <TableCell className="whitespace-normal wrap-anywhere">
                    <p className="font-medium">{invite.email ?? 'Anyone with the link'}</p>
                    {invite.requireEmailMatch && <p className="text-sm text-muted-foreground">Email must match</p>}
                    {isMobile && (
                      <p className="text-sm text-muted-foreground">
                        {ROLE_LABELS[invite.role]}, expires {formatExpiry(invite.expiresAt)}
                      </p>
                    )}
                  </TableCell>
                  {!isMobile && <TableCell className="whitespace-nowrap">{ROLE_LABELS[invite.role]}</TableCell>}
                  <TableCell>
                    <Badge variant={STATE_VARIANTS[state]}>{INVITE_STATE_LABELS[state]}</Badge>
                  </TableCell>
                  {!isMobile && <TableCell className="whitespace-nowrap">{formatExpiry(invite.expiresAt)}</TableCell>}
                  <TableCell>
                    <InviteActions
                      invite={invite}
                      isRevoking={revoke.isPending && revoke.variables === invite.id}
                      onRevoke={() => handleRevoke(invite)}
                    />
                  </TableCell>
                </TableRow>
              )
            })}
          </TableBody>
        </Table>
      </div>
      <Pager label="Pages of invites" pagination={pagination} onPageChange={onPageChange} />
    </div>
  )
}
