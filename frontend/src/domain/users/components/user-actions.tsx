import type { UserDto, UserDtoAllowedActionsEnumKey } from '@/api/generated/models/types/user-dto'
import { Button } from '@/components/ui/button'

export type UserAction = UserDtoAllowedActionsEnumKey

// A Record of the generated union, so a new or renamed action on the Server fails the build until it has a label.
const LABELS: Record<UserAction, string> = {
  Update: 'Edit',
  UpdateRole: 'Change role',
  AddPermission: 'Grant permission',
  RemovePermission: 'Remove permission',
  Delete: 'Delete',
}

interface UserActionsProps {
  user: UserDto
  onAction: (action: UserAction) => void
}

/** A button for each action that the Server allows the caller to send for the user. */
export function UserActions({ user, onAction }: UserActionsProps) {
  if (user.allowedActions.length === 0) {
    return null
  }

  return (
    <div role="group" aria-label={`Actions for ${user.fullName}`} className="flex flex-wrap gap-2">
      {user.allowedActions.map((action) => (
        <Button
          key={action}
          variant={action === 'Delete' ? 'destructive' : 'outline'}
          onPress={() => onAction(action)}
        >
          {LABELS[action]}
        </Button>
      ))}
    </div>
  )
}
