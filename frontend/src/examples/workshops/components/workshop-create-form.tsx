import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import * as z from 'zod'

import type { WorkshopForCreationDto } from '@/api/generated/models/types/workshop-for-creation-dto'
import { workshopForCreationDtoSchema } from '@/api/generated/models/zod/workshop-for-creation-dto'
import { Button } from '@/components/ui/button'
import { Form } from '@/components/ui/form'
import { RefusedSaveAlert } from '@/examples/components/refused-save-alert'
import { useServerErrors } from '@/examples/components/use-server-errors'
import { useVenue } from '@/examples/venues/apis/get-venue'
import { VenuePicker } from '@/examples/venues/components/venue-picker'
import { parseTimeZone } from '@/lib/zoned-time'

import { WorkshopFields } from './workshop-fields'
import { WORKSHOP_FIELD_NAMES, limitsOf, startInGapMessage, toWorkshopBody, workshopFieldsSchema } from './workshop-fields-schema'

const contract = workshopForCreationDtoSchema.shape
const schema = workshopFieldsSchema(contract).extend({ venueId: z.string().min(1, 'Pick a venue.') })
const limits = limitsOf(contract)
const FIELD_NAMES = ['venueId', ...WORKSHOP_FIELD_NAMES] as const

interface WorkshopCreateFormProps {
  venueId?: string
  onSave: (body: WorkshopForCreationDto) => Promise<unknown>
}

export function WorkshopCreateForm({ venueId = '', onSave }: WorkshopCreateFormProps) {
  const form = useForm({ resolver: zodResolver(schema), defaultValues: { venueId, title: '' } })
  const serverErrors = useServerErrors(form, FIELD_NAMES)
  const venue = useVenue(form.watch('venueId') || undefined)
  const zone = venue.data && parseTimeZone(venue.data.timeZone)

  const handleSubmit = form.handleSubmit(async (values) => {
    serverErrors.clear()
    if (!zone) {
      form.setError('venueId', { message: 'The venue did not load. Pick it again.' })
      return
    }
    const body = toWorkshopBody(values, zone)
    if (!body) {
      form.setError('startsAt', { message: startInGapMessage(zone) })
      return
    }
    try {
      await onSave({ venueId: values.venueId, ...body })
    } catch (error) {
      serverErrors.show(error)
    }
  })

  return (
    <Form {...form}>
      <form onSubmit={handleSubmit} noValidate aria-label="New workshop" className="flex max-w-lg flex-col gap-5">
        <Controller
          control={form.control}
          name="venueId"
          render={({ field, fieldState }) => (
            <VenuePicker
              value={field.value}
              onChange={field.onChange}
              onBlur={field.onBlur}
              errorMessage={fieldState.error?.message}
            />
          )}
        />
        <WorkshopFields limits={limits} zone={zone} />
        <RefusedSaveAlert problem={serverErrors.problem} />
        <Button type="submit" className="self-start" isPending={form.formState.isSubmitting}>
          Add workshop
        </Button>
      </form>
    </Form>
  )
}
