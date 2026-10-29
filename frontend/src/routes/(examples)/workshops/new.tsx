import { createFileRoute, useNavigate } from '@tanstack/react-router'
import * as z from 'zod'

import type { WorkshopForCreationDto } from '@/api/generated/models/types/workshop-for-creation-dto'
import { Notification } from '@/components/notification'
import { Link } from '@/components/ui/link'
import { PageHeading } from '@/components/page-heading'
import { useVenue } from '@/examples/venues/apis/get-venue'
import { useAddWorkshop } from '@/examples/workshops/apis/add-workshop'
import { WorkshopCreateForm } from '@/examples/workshops/components/workshop-create-form'

export const Route = createFileRoute('/(examples)/workshops/new')({
  validateSearch: z.object({ venueId: z.string().optional().catch(undefined) }),
  component: NewWorkshopPage,
  staticData: { breadcrumb: 'New workshop' },
})

function NewWorkshopPage() {
  const { venueId } = Route.useSearch()
  const linkedVenue = useVenue(venueId, { suppressErrorToast: true })
  const add = useAddWorkshop()
  const navigate = useNavigate()

  const handleSave = async (body: WorkshopForCreationDto) => {
    const workshop = await add.mutateAsync(body)
    Notification.success('Workshop added')
    await navigate({ to: '/workshops/$id', params: { id: workshop.id } })
  }

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8">
      <Link to="/workshops" className="text-sm text-muted-foreground underline-offset-4 hover:underline">
        All workshops
      </Link>
      <PageHeading>New workshop</PageHeading>
      {venueId && linkedVenue.isPending ? (
        <p role="status" className="text-muted-foreground">
          Loading venue
        </p>
      ) : (
        <WorkshopCreateForm venueId={linkedVenue.isSuccess ? venueId : undefined} onSave={handleSave} />
      )}
      <p className="text-sm text-muted-foreground">
        No venue for it yet?{' '}
        <Link to="/venues/new" className="underline underline-offset-4 hover:text-primary">
          Add a venue
        </Link>
        .
      </p>
    </div>
  )
}
