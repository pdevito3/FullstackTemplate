import { useId } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'

import type { VenueForCreationDto } from '@/api/generated/models/types/venue-for-creation-dto'
import { venueForCreationDtoSchema } from '@/api/generated/models/zod/venue-for-creation-dto'
import { Button } from '@/components/ui/button'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Form } from '@/components/ui/form'
import { Input } from '@/components/ui/input'
import { RefusedSaveAlert } from '@/examples/components/refused-save-alert'
import { useServerErrors } from '@/examples/components/use-server-errors'
import { stringWithMaxLengthOf } from '@/lib/api-contract'
import { browserTimeZone } from '@/lib/zoned-time'

const contract = venueForCreationDtoSchema.shape
const schema = venueForCreationDtoSchema.extend({
  name: stringWithMaxLengthOf(contract.name).min(1, 'Enter a name.'),
  timeZone: stringWithMaxLengthOf(contract.timeZone).min(1, 'Enter a time zone.'),
})
const FIELD_NAMES = ['name', 'timeZone'] as const

interface VenueCreateFormProps {
  onSave: (body: VenueForCreationDto) => Promise<unknown>
}

/** The time zone starts as the zone of the browser. The Server checks that the zone is a real IANA zone. */
export function VenueCreateForm({ onSave }: VenueCreateFormProps) {
  const form = useForm({
    resolver: zodResolver(schema),
    defaultValues: { name: '', timeZone: browserTimeZone() },
  })
  const serverErrors = useServerErrors(form, FIELD_NAMES)
  const id = useId()
  const { errors } = form.formState

  const handleSubmit = form.handleSubmit(async (values) => {
    serverErrors.clear()
    try {
      await onSave(values)
    } catch (error) {
      serverErrors.show(error)
    }
  })

  return (
    <Form {...form}>
      <form onSubmit={handleSubmit} noValidate aria-label="New venue" className="flex max-w-lg flex-col gap-5">
        <Field data-invalid={!!errors.name || undefined}>
          <FieldLabel htmlFor={`${id}-name`}>Name</FieldLabel>
          <Input
            id={`${id}-name`}
            aria-invalid={!!errors.name || undefined}
            aria-describedby={`${id}-name-error`}
            {...form.register('name')}
          />
          <FieldError id={`${id}-name-error`} errors={[errors.name]} />
        </Field>
        <Field data-invalid={!!errors.timeZone || undefined}>
          <FieldLabel htmlFor={`${id}-time-zone`}>Time zone</FieldLabel>
          <Input
            id={`${id}-time-zone`}
            aria-invalid={!!errors.timeZone || undefined}
            aria-describedby={`${id}-time-zone-hint ${id}-time-zone-error`}
            {...form.register('timeZone')}
          />
          <FieldDescription id={`${id}-time-zone-hint`}>An IANA time zone, for example America/Chicago.</FieldDescription>
          <FieldError id={`${id}-time-zone-error`} errors={[errors.timeZone]} />
        </Field>
        <RefusedSaveAlert problem={serverErrors.problem} />
        <Button type="submit" className="self-start" isPending={form.formState.isSubmitting}>
          Add venue
        </Button>
      </form>
    </Form>
  )
}
