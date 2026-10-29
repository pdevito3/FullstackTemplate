import type { ReactElement } from 'react'

import type { WorkshopDto, WorkshopDtoAllowedActionsEnumKey } from '@/api/generated/models/types/workshop-dto'
import { Button } from '@/components/ui/button'
import { buttonVariants } from '@/components/ui/button-variants'
import { Link } from '@/components/ui/link'
import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
} from '@/components/ui/responsive-dialog'

export type WorkshopCommand = Exclude<WorkshopDtoAllowedActionsEnumKey, 'Update'>

const LABELS: Record<WorkshopCommand, string> = {
  Schedule: 'Schedule',
  Cancel: 'Cancel workshop',
  Complete: 'Mark completed',
}

const isCommand = (action: WorkshopDtoAllowedActionsEnumKey): action is WorkshopCommand => action !== 'Update'

interface WorkshopActionsProps {
  workshop: WorkshopDto
  pendingAction?: WorkshopCommand
  onAction: (action: WorkshopCommand) => void
}

export function WorkshopActions({ workshop, pendingAction, onAction }: WorkshopActionsProps) {
  if (workshop.allowedActions.length === 0) {
    return null
  }

  return (
    <div role="group" aria-label={`Actions for ${workshop.title}`} className="flex flex-wrap gap-2">
      {workshop.allowedActions.includes('Update') && (
        <Link to="/workshops/$id/edit" params={{ id: workshop.id }} className={buttonVariants({ variant: 'outline' })}>
          Edit
        </Link>
      )}
      {workshop.allowedActions.filter(isCommand).map((action) => {
        const button = (
          <Button
            key={action}
            variant={action === 'Cancel' ? 'destructive' : 'outline'}
            isPending={pendingAction === action}
            isDisabled={pendingAction !== undefined && pendingAction !== action}
            onPress={action === 'Cancel' ? undefined : () => onAction(action)}
          >
            {LABELS[action]}
          </Button>
        )
        return action === 'Cancel' ? (
          <ConfirmCancel key={action} onConfirm={() => onAction(action)}>
            {button}
          </ConfirmCancel>
        ) : (
          button
        )
      })}
    </div>
  )
}

/** A cancel cannot be undone, so the Cancel workshop button opens a dialog that asks first. */
function ConfirmCancel({ children, onConfirm }: { children: ReactElement; onConfirm: () => void }) {
  return (
    <ResponsiveDialogTrigger>
      {children}
      <ResponsiveDialog>
        <ResponsiveDialogHeader>
          <ResponsiveDialogTitle>Cancel this workshop?</ResponsiveDialogTitle>
          <ResponsiveDialogDescription>
            After you cancel it, you cannot schedule, change, or complete it.
          </ResponsiveDialogDescription>
        </ResponsiveDialogHeader>
        <ResponsiveDialogFooter>
          <ResponsiveDialogClose>Keep workshop</ResponsiveDialogClose>
          <ResponsiveDialogClose variant="destructive" onPress={onConfirm}>
            Cancel workshop
          </ResponsiveDialogClose>
        </ResponsiveDialogFooter>
      </ResponsiveDialog>
    </ResponsiveDialogTrigger>
  )
}
