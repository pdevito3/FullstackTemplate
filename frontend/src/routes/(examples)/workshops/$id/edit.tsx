import { createFileRoute, useNavigate } from '@tanstack/react-router'

import type { WorkshopForUpdateDto } from '@/api/generated/models/types/workshop-for-update-dto'
import { Notification } from '@/components/notification'
import { Link } from '@/components/ui/link'
import { useWorkshop } from '@/examples/workshops/apis/get-workshop'
import { useUpdateWorkshop } from '@/examples/workshops/apis/update-workshop'
import { QueryError } from '@/components/query-error'
import { WorkshopEditForm } from '@/examples/workshops/components/workshop-edit-form'

export const Route = createFileRoute('/(examples)/workshops/$id/edit')({
  component: EditWorkshopPage,
  staticData: { breadcrumb: 'Edit workshop' },
})

function EditWorkshopPage() {
  const { id } = Route.useParams()
  const workshop = useWorkshop(id)
  const update = useUpdateWorkshop(id)
  const navigate = useNavigate()

  if (workshop.isPending) {
    return <p role="status" aria-label="Loading workshop" className="px-4 py-8 text-muted-foreground">Loading workshop</p>
  }

  if (workshop.isError) {
    return (
      <div className="mx-auto w-full max-w-4xl px-4 py-8">
        <QueryError title="The workshop did not load." error={workshop.error} onRetry={() => workshop.refetch()} />
      </div>
    )
  }

  const handleSave = async (body: WorkshopForUpdateDto) => {
    await update.mutateAsync(body)
    Notification.success('Workshop saved')
    await navigate({ to: '/workshops/$id', params: { id } })
  }

  const handleLoadLatest = async () => {
    const result = await workshop.refetch()
    return result.status === 'success' ? result.data : undefined
  }

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8">
      <Link to="/workshops/$id" params={{ id }} className="text-sm text-muted-foreground underline-offset-4 hover:underline">
        Back to the workshop
      </Link>
      <h1 className="text-3xl font-bold tracking-tight">Edit workshop</h1>
      {workshop.data.allowedActions.includes('Update') ? (
        <WorkshopEditForm workshop={workshop.data} onSave={handleSave} onLoadLatest={handleLoadLatest} />
      ) : (
        <p className="text-muted-foreground">
          {workshop.data.status === 'Draft'
            ? 'You cannot manage workshops. Ask a tenant admin for access.'
            : `A ${workshop.data.status.toLowerCase()} workshop cannot be changed.`}
        </p>
      )}
    </div>
  )
}
