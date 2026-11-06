import { useQueryClient } from '@tanstack/react-query'
import { createFileRoute, useNavigate } from '@tanstack/react-router'
import { useRef } from 'react'

import { Notification } from '@/components/notification'
import { PageHeading } from '@/components/page-heading'
import { QueryError } from '@/components/query-error'
import { Link } from '@/components/ui/link'
import { useMembership } from '@/domain/membership/apis/get-membership'
import { StartTenantPrompt } from '@/domain/membership/components/require-tenant'
import { useDeleteUser } from '@/domain/users/apis/delete-user'
import { useUser } from '@/domain/users/apis/get-user'
import { useUpdateUserRole } from '@/domain/users/apis/update-user-role'
import { UserKeys } from '@/domain/users/apis/user.keys'
import { UserActions } from '@/domain/users/components/user-actions'
import { ROLE_LABELS } from '@/domain/users/components/user-role'
import { leaveDeletedRecord } from '@/lib/leave-deleted-record'

export const Route = createFileRoute('/users/$id/')({
  component: UserPage,
  staticData: { breadcrumb: 'User' },
})

function UserPage() {
  const { id } = Route.useParams()
  const membership = useMembership().status
  // The Server refuses this request for a user with no tenant, and its text repeats the notice of the layout.
  const user = useUser(id, { enabled: membership !== 'loading' && membership !== 'no-tenant' })
  const changeRole = useUpdateUserRole(id)
  const remove = useDeleteUser()
  const queryClient = useQueryClient()
  const navigate = useNavigate()
  const headingRef = useRef<HTMLHeadingElement>(null)

  if (membership === 'no-tenant') {
    return (
      <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8">
        <PageHeading>User</PageHeading>
        <StartTenantPrompt purpose="to manage its users." />
      </div>
    )
  }

  if (user.isPending) {
    return <p role="status" aria-label="Loading user" className="px-4 py-8 text-muted-foreground">Loading user</p>
  }

  if (user.isError) {
    return (
      <div className="mx-auto w-full max-w-4xl px-4 py-8">
        <QueryError title="The user did not load." error={user.error} onRetry={() => user.refetch()} />
      </div>
    )
  }

  const { fullName, email, username, role } = user.data

  const handleDelete = () =>
    remove.mutate(id, {
      onSuccess: () => {
        Notification.success('User deleted')
        return leaveDeletedRecord(queryClient, {
          detail: UserKeys.detail(id),
          lists: UserKeys.lists(),
          leave: () => navigate({ to: '/users' }),
        })
      },
      onError: () => headingRef.current?.focus(),
    })

  const pendingAction = (changeRole.isPending && 'UpdateRole') || (remove.isPending && 'Delete') || undefined

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8">
      <Link to="/users" className="text-sm text-muted-foreground underline-offset-4 hover:underline">
        All users
      </Link>
      <PageHeading ref={headingRef}>{fullName}</PageHeading>
      <section aria-label="Details">
        <dl className="grid max-w-md grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm wrap-anywhere">
          <dt className="text-muted-foreground">Email</dt>
          <dd>{email}</dd>
          <dt className="text-muted-foreground">Username</dt>
          <dd>{username}</dd>
          <dt className="text-muted-foreground">Role</dt>
          <dd>{ROLE_LABELS[role]}</dd>
        </dl>
      </section>
      <UserActions
        user={user.data}
        pendingAction={pendingAction}
        onChangeRole={(newRole) => changeRole.mutate(newRole, { onSettled: () => headingRef.current?.focus() })}
        onDelete={handleDelete}
      />
    </div>
  )
}
