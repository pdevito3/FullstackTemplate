import type { ReactElement } from 'react'

import type { InviteDto } from '@/api/generated/models/types/invite-dto'
import { Button } from '@/components/ui/button'
import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
} from '@/components/ui/responsive-dialog'

interface InviteActionsProps {
  invite: InviteDto
  isRevoking: boolean
  onRevoke: () => void
}

type ActionControl = (props: InviteActionsProps) => ReactElement

const CONTROLS: Record<InviteDto['allowedActions'][number], ActionControl> = {
  Revoke: ConfirmRevoke,
}

export function InviteActions({ invite, isRevoking, onRevoke }: InviteActionsProps) {
  const controls = invite.allowedActions.map((action) => ({ action, Control: CONTROLS[action] }))

  return (
    <div className="flex justify-end gap-2">
      {controls.map(({ action, Control }) => (
        <Control key={action} invite={invite} isRevoking={isRevoking} onRevoke={onRevoke} />
      ))}
    </div>
  )
}

function ConfirmRevoke({ invite, isRevoking, onRevoke }: InviteActionsProps) {
  return (
    <ResponsiveDialogTrigger>
      <Button variant="outline" size="sm" isPending={isRevoking} aria-label={`Revoke the invite for ${invite.email ?? 'anyone with the link'}`}>
        Revoke
      </Button>
      <ResponsiveDialog>
        <ResponsiveDialogHeader>
          <ResponsiveDialogTitle>{invite.email ? `Revoke the invite for ${invite.email}?` : 'Revoke this invite?'}</ResponsiveDialogTitle>
          <ResponsiveDialogDescription>The link stops working. You cannot undo a revoke.</ResponsiveDialogDescription>
        </ResponsiveDialogHeader>
        <ResponsiveDialogFooter className="mt-4">
          <ResponsiveDialogClose>Keep invite</ResponsiveDialogClose>
          <ResponsiveDialogClose variant="destructive" onPress={onRevoke}>
            Revoke invite
          </ResponsiveDialogClose>
        </ResponsiveDialogFooter>
      </ResponsiveDialog>
    </ResponsiveDialogTrigger>
  )
}
