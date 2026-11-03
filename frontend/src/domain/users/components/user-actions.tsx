import { zodResolver } from '@hookform/resolvers/zod'
import type { ReactElement } from 'react'
import { Controller, useForm } from 'react-hook-form'

import type { UpdateUserRoleDto } from '@/api/generated/models/types/update-user-role-dto'
import type { UserDto, UserDtoAllowedActionsEnumKey } from '@/api/generated/models/types/user-dto'
import { updateUserRoleDtoSchema } from '@/api/generated/models/zod/update-user-role-dto'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import {
  ResponsiveDialog,
  ResponsiveDialogClose,
  ResponsiveDialogDescription,
  ResponsiveDialogFooter,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
  ResponsiveDialogTrigger,
} from '@/components/ui/responsive-dialog'
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select'

import { isUserRole, ROLE_LABELS, USER_ROLES, type UserRole } from './user-role'

export type UserAction = UserDtoAllowedActionsEnumKey

interface UserActionsProps {
  user: UserDto
  pendingAction?: UserAction
  onChangeRole: (role: UserRole) => void
  onDelete: () => void
}

type ActionControl = (props: UserActionsProps) => ReactElement

const CONTROLS: Record<UserAction, ActionControl | null> = {
  Update: null,
  UpdateRole: ChangeRole,
  AddPermission: null,
  RemovePermission: null,
  Delete: ConfirmDelete,
}

export function UserActions({ user, pendingAction, onChangeRole, onDelete }: UserActionsProps) {
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
        <Control key={action} user={user} pendingAction={pendingAction} onChangeRole={onChangeRole} onDelete={onDelete} />
      ))}
    </div>
  )
}

const buttonState = (action: UserAction, pendingAction: UserAction | undefined) => ({
  isPending: pendingAction === action,
  isDisabled: pendingAction !== undefined && pendingAction !== action,
})

function ChangeRole({ user, pendingAction, onChangeRole }: UserActionsProps) {
  return (
    <ResponsiveDialogTrigger>
      <Button variant="outline" {...buttonState('UpdateRole', pendingAction)}>
        Change role
      </Button>
      <ResponsiveDialog>
        <ChangeRoleForm user={user} onChangeRole={onChangeRole} />
      </ResponsiveDialog>
    </ResponsiveDialogTrigger>
  )
}

function ChangeRoleForm({ user, onChangeRole }: Pick<UserActionsProps, 'user' | 'onChangeRole'>) {
  const form = useForm<UpdateUserRoleDto>({
    resolver: zodResolver(updateUserRoleDtoSchema),
    defaultValues: { role: user.role },
  })
  const handleConfirm = () =>
    void form.handleSubmit(({ role }) => {
      if (role !== user.role) onChangeRole(role)
    })()

  return (
    <>
      <ResponsiveDialogHeader>
        <ResponsiveDialogTitle>Change the role of {user.fullName}</ResponsiveDialogTitle>
        <ResponsiveDialogDescription>
          The new role replaces the permissions of the user with the permissions of that role.
        </ResponsiveDialogDescription>
      </ResponsiveDialogHeader>
      <div className="pt-3 pb-4">
        <Controller
          control={form.control}
          name="role"
          render={({ field }) => (
            <Select
              value={field.value}
              onChange={(key) => {
                if (isUserRole(key)) field.onChange(key)
              }}
              className="flex w-full flex-col gap-2"
            >
              <Label>Role</Label>
              <SelectTrigger ref={field.ref} className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {USER_ROLES.map((role) => (
                  <SelectItem key={role} id={role}>
                    {ROLE_LABELS[role]}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        />
      </div>
      <ResponsiveDialogFooter>
        <ResponsiveDialogClose>Keep role</ResponsiveDialogClose>
        <ResponsiveDialogClose variant="default" onPress={handleConfirm}>
          Change role
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
