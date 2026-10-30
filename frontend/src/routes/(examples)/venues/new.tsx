import { createFileRoute, useNavigate } from '@tanstack/react-router'

import type { VenueForCreationDto } from '@/api/generated/models/types/venue-for-creation-dto'
import { Notification } from '@/components/notification'
import { Link } from '@/components/ui/link'
import { useAddVenue } from '@/examples/venues/apis/add-venue'
import { VenueCreateForm } from '@/examples/venues/components/venue-create-form'
import { PageHeading } from '@/components/page-heading'

export const Route = createFileRoute('/(examples)/venues/new')({
  component: NewVenuePage,
  staticData: { breadcrumb: 'New venue' },
})

function NewVenuePage() {
  const add = useAddVenue()
  const navigate = useNavigate()

  const handleSave = async (body: VenueForCreationDto) => {
    const venue = await add.mutateAsync(body)
    Notification.success('Venue added')
    await navigate({ to: '/workshops/new', search: { venueId: venue.id } })
  }

  return (
    <div className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-8">
      <Link to="/workshops" className="text-sm text-muted-foreground underline-offset-4 hover:underline">
        All workshops
      </Link>
      <PageHeading>New venue</PageHeading>
      <VenueCreateForm onSave={handleSave} />
    </div>
  )
}
