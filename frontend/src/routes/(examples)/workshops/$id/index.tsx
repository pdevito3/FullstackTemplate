import { createFileRoute } from '@tanstack/react-router'

import { Link } from '@/components/ui/link'
import { useVenue } from '@/examples/venues/apis/get-venue'
import { useWorkshop } from '@/examples/workshops/apis/get-workshop'
import { useSendWorkshopAction } from '@/examples/workshops/apis/send-workshop-action'
import { formatStart } from '@/examples/workshops/components/format-start'
import { QueryError } from '@/examples/components/query-error'
import { WorkshopActions } from '@/examples/workshops/components/workshop-actions'
import { WorkshopStatusBadge } from '@/examples/workshops/components/workshop-status-badge'

export const Route = createFileRoute('/(examples)/workshops/$id/')({
  component: WorkshopPage,
  staticData: { breadcrumb: 'Workshop' },
})

function WorkshopPage() {
  const { id } = Route.useParams()
  const workshop = useWorkshop(id)
  const venue = useVenue(workshop.data?.venueId)
  const action = useSendWorkshopAction(id)

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

  const { title, status, capacity } = workshop.data

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8">
      <Link to="/workshops" className="text-sm text-muted-foreground underline-offset-4 hover:underline">
        All workshops
      </Link>
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="min-w-0 text-3xl font-bold tracking-tight wrap-anywhere">{title}</h1>
        <WorkshopStatusBadge status={status} />
      </div>
      <section aria-label="Details">
        <dl className="grid max-w-md grid-cols-[auto_1fr] gap-x-6 gap-y-2 text-sm wrap-anywhere">
          <dt className="text-muted-foreground">Starts</dt>
          <dd>{formatStart(workshop.data)}</dd>
          <dt className="text-muted-foreground">Capacity</dt>
          <dd>{capacity}</dd>
          <dt className="text-muted-foreground">Venue</dt>
          <dd>{venue.data?.name ?? (venue.isError ? 'Not available' : 'Loading')}</dd>
        </dl>
      </section>
      <WorkshopActions
        workshop={workshop.data}
        pendingAction={action.isPending ? action.variables : undefined}
        onAction={(command) => action.mutate(command)}
      />
    </div>
  )
}
