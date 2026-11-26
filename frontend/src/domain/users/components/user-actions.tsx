import { zodResolver } from '@hookform/resolvers/zod'
import type { ReactElement } from 'react'
import { Controller, useForm } from 'react-hook-form'

import type { UpdateUserRolesDto } from '@/api/generated/models/types/update-user-roles-dto'
import type { UserDto, UserDtoAllowedActionsEnumKey } from '@/api/generated/models/types/user-dto'
import { updateUserRolesDtoSchema } from '@/api/generated/models/zod/update-user-roles-dto'
import { Button } from '@/components/ui/button'
import { Checkbox, CheckboxGroupWithLabel } from '@/components/ui/checkbox'
import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
} from '@/components/ui/responsive-dialog'

import { isUserRole, ROLE_LABELS, USER_ROLES, type UserRole } from './user-role'

export type UserAction = UserDtoAllowedActionsEnumKey

interface UserActionsProps {
  user: UserDto
  pendingAction?: UserAction
  onChangeRoles: (roles: UserRole[]) => void
  onDelete: () => void
}

type ActionControl = (props: UserActionsProps) => ReactElement

const CONTROLS: Record<UserAction, ActionControl | null> = {
  Update: null,
  UpdateRoles: ChangeRoles,
  AddPermission: null,
  RemovePermission: null,
  Delete: ConfirmDelete,
}

export function UserActions({ user, pendingAction, onChangeRoles, onDelete }: UserActionsProps) {
  const controls = user.allowedActions.flatMap((action) => {
    const control = CONTROLS[action]
    return control ? [{ action, Control: control }] : []
  })

  if (controls.length === 0) {
    return null
  }

  return (
    <div role="group" aria-label={`Actions for ${user.fullName}`} className="flex flex-wrap gap-2">
      {controls.map(({ action, Control }) => (
        <Control key={action} user={user} pendingAction={pendingAction} onChangeRoles={onChangeRoles} onDelete={onDelete} />
      ))}
    </div>
  )
}

const buttonState = (action: UserAction, pendingAction: UserAction | undefined) => ({
  isPending: pendingAction === action,
  isDisabled: pendingAction !== undefined && pendingAction !== action,
})

function ChangeRoles({ user, pendingAction, onChangeRoles }: UserActionsProps) {
  return (
    <ResponsiveDialogTrigger>
      <Button variant="outline" {...buttonState('UpdateRoles', pendingAction)}>
        Change roles
      </Button>
      <ResponsiveDialog>
        <ChangeRolesForm user={user} onChangeRoles={onChangeRoles} />
      </ResponsiveDialog>
    </ResponsiveDialogTrigger>
  )
}

const sameRoles = (a: readonly UserRole[], b: readonly UserRole[]) =>
  a.length === b.length && a.every((role) => b.includes(role))

function ChangeRolesForm({ user, onChangeRoles }: Pick<UserActionsProps, 'user' | 'onChangeRoles'>) {
  const form = useForm<UpdateUserRolesDto>({
    resolver: zodResolver(updateUserRolesDtoSchema),
    defaultValues: { roles: user.roles },
  })
  const handleConfirm = () =>
    void form.handleSubmit(({ roles }) => {
      if (!sameRoles(roles, user.roles)) onChangeRoles(roles)
    })()

  return (
    <>
      <ResponsiveDialogHeader>
        <ResponsiveDialogTitle>Change the roles of {user.fullName}</ResponsiveDialogTitle>
        <ResponsiveDialogDescription>
          The user gets the permissions of each role. A permission that someone added or removed by hand stays as it is.
        </ResponsiveDialogDescription>
      </ResponsiveDialogHeader>
      <div className="pt-3 pb-4">
        <Controller
          control={form.control}
          name="roles"
          render={({ field }) => (
            <CheckboxGroupWithLabel
              label="Roles"
              value={field.value}
              onChange={(keys) => field.onChange(keys.filter(isUserRole))}
            >
              {USER_ROLES.map((role) => (
                <Checkbox key={role} value={role}>
                  {ROLE_LABELS[role]}
                </Checkbox>
              ))}
            </CheckboxGroupWithLabel>
          )}
        />
      </div>
      <ResponsiveDialogFooter>
        <ResponsiveDialogClose>Keep roles</ResponsiveDialogClose>
        <ResponsiveDialogClose variant="default" onPress={handleConfirm}>
          Change roles
        </ResponsiveDialogClose>
      </ResponsiveDialogFooter>
    </>
  )
}

function ConfirmDelete({ user, pendingAction, onDelete }: UserActionsProps) {
  return (
    <ResponsiveDialogTrigger>
      <Button variant="destructive" {...buttonState('Delete', pendingAction)}>
        Delete
      </Button>
      <ResponsiveDialog>
        <ResponsiveDialogHeader>
          <ResponsiveDialogTitle>Delete {user.fullName}?</ResponsiveDialogTitle>
          <ResponsiveDialogDescription>
            The user loses access to this tenant. You cannot undo a delete.
          </ResponsiveDialogDescription>
        </ResponsiveDialogHeader>
        <ResponsiveDialogFooter>
          <ResponsiveDialogClose>Keep user</ResponsiveDialogClose>
          <ResponsiveDialogClose variant="destructive" onPress={onDelete}>
            Delete user
          </ResponsiveDialogClose>
        </ResponsiveDialogFooter>
      </ResponsiveDialog>
    </ResponsiveDialogTrigger>
  )
}
