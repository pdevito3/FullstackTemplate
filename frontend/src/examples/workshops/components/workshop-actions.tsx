import type { WorkshopDto, WorkshopDtoAllowedActionsEnumKey } from '@/api/generated/models/types/workshop-dto'
import { Button } from '@/components/ui/button'
import { buttonVariants } from '@/components/ui/button-variants'
import { Link } from '@/components/ui/link'

/** An action that the page sends at once. `Update` opens the edit form instead. */
export type WorkshopCommand = Exclude<WorkshopDtoAllowedActionsEnumKey, 'Update'>

// A Record of the generated union, so a new or renamed action on the Server fails the build until it has a label.
const LABELS: Record<WorkshopCommand, string> = {
  Schedule: 'Schedule',
  Cancel: 'Cancel workshop',
  Complete: 'Mark completed',
}

const isCommand = (action: WorkshopDtoAllowedActionsEnumKey): action is WorkshopCommand => action !== 'Update'

interface WorkshopActionsProps {
  workshop: WorkshopDto
  /** The action that waits for the Server, if any. The buttons wait with it. */
  pendingAction?: WorkshopCommand
  onAction: (action: WorkshopCommand) => void
}

/** The edit link and a button for each action that the Server allows the caller to send for the workshop now. */
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
      {workshop.allowedActions.filter(isCommand).map((action) => (
        <Button
          key={action}
          variant={action === 'Cancel' ? 'destructive' : 'outline'}
          isPending={pendingAction === action}
          isDisabled={pendingAction !== undefined && pendingAction !== action}
          onPress={() => onAction(action)}
        >
          {LABELS[action]}
        </Button>
      ))}
    </div>
  )
}
