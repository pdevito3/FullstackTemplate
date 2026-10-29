import { zodResolver } from '@hookform/resolvers/zod'
import { Controller, useForm } from 'react-hook-form'
import * as z from 'zod'

import type { WorkshopForCreationDto } from '@/api/generated/models/types/workshop-for-creation-dto'
import { workshopForCreationDtoSchema } from '@/api/generated/models/zod/workshop-for-creation-dto'
import { Button } from '@/components/ui/button'
import { Form } from '@/components/ui/form'
import { RefusedSaveAlert } from '@/examples/components/refused-save-alert'
import { useServerErrors } from '@/examples/components/use-server-errors'
import { VenuePicker } from '@/examples/venues/components/venue-picker'

import { WorkshopFields } from './workshop-fields'
import { WORKSHOP_FIELD_NAMES, limitsOf, toWorkshopBody, workshopFieldsSchema } from './workshop-fields-schema'

const contract = workshopForCreationDtoSchema.shape
const schema = workshopFieldsSchema(contract).extend({ venueId: z.string().min(1, 'Pick a venue.') })
const limits = limitsOf(contract)
const FIELD_NAMES = ['venueId', ...WORKSHOP_FIELD_NAMES] as const

interface WorkshopCreateFormProps {
  /** The venue that the form starts with, for example the venue that the user just added. */
  venueId?: string
  onSave: (body: WorkshopForCreationDto) => Promise<unknown>
}

export function WorkshopCreateForm({ venueId = '', onSave }: WorkshopCreateFormProps) {
  const form = useForm({ resolver: zodResolver(schema), defaultValues: { venueId, title: '' } })
  const serverErrors = useServerErrors(form, FIELD_NAMES)

  // A create keeps the input of the user on any refusal, and the form has no version to load.
  const handleSubmit = form.handleSubmit(async (values) => {
    serverErrors.clear()
    try {
      await onSave({ venueId: values.venueId, ...toWorkshopBody(values) })
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
        <WorkshopFields limits={limits} />
        <RefusedSaveAlert problem={serverErrors.problem} />
        <Button type="submit" className="self-start" isPending={form.formState.isSubmitting}>
          Add workshop
        </Button>
      </form>
    </Form>
  )
}
