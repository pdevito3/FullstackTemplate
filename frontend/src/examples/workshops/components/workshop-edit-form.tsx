import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'

import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import type { WorkshopForUpdateDto } from '@/api/generated/models/types/workshop-for-update-dto'
import { workshopForUpdateDtoSchema } from '@/api/generated/models/zod/workshop-for-update-dto'
import { RefusedSaveAlert } from '@/components/refused-save-alert'
import { LatestLoadedNote, StaleVersionAlert } from '@/components/stale-version-alert'
import { Button } from '@/components/ui/button'
import { Form } from '@/components/ui/form'
import { useLoadLatest } from '@/hooks/use-load-latest'
import { useServerErrors } from '@/hooks/use-server-errors'
import { focusFirstError } from '@/lib/focus-first-error'
import { parseTimeZone, springGapMessage, wallClockAt } from '@/lib/zoned-time'

import { WorkshopFields } from './workshop-fields'
import { WORKSHOP_FIELD_NAMES, limitsOf, toWorkshopBody, workshopFieldsSchema, type WorkshopFieldName } from './workshop-fields-schema'

const contract = workshopForUpdateDtoSchema.shape
const schema = workshopFieldsSchema(contract).extend({ version: contract.version })
const limits = limitsOf(contract)

const FIELD_LABELS: Record<WorkshopFieldName, string> = { title: 'Title', startsAt: 'Starts at', capacity: 'Capacity' }

function valuesOf(workshop: WorkshopDto) {
  return {
    title: workshop.title,
    startsAt: wallClockAt(new Date(workshop.startsAt), parseTimeZone(workshop.venueTimeZone)),
    capacity: workshop.capacity,
    version: workshop.version,
  }
}

interface WorkshopEditFormProps {
  workshop: WorkshopDto
  onSave: (body: WorkshopForUpdateDto) => Promise<unknown>
  onLoadLatest: () => Promise<WorkshopDto | undefined>
}

export function WorkshopEditForm({ workshop, onSave, onLoadLatest }: WorkshopEditFormProps) {
  const form = useForm({ resolver: zodResolver(schema), defaultValues: valuesOf(workshop), shouldFocusError: false })
  const serverErrors = useServerErrors(form, WORKSHOP_FIELD_NAMES)
  const latest = useLoadLatest(form, WORKSHOP_FIELD_NAMES)
  const zone = parseTimeZone(workshop.venueTimeZone)

  const handleSubmit = form.handleSubmit(async (values) => {
    serverErrors.clear()
    latest.clear()
    const body = toWorkshopBody(values, zone)
    if (!body) {
      form.setError('startsAt', { message: springGapMessage(zone) }, { shouldFocus: true })
      return
    }
    try {
      await onSave({ ...body, version: values.version })
    } catch (error) {
      serverErrors.show(error)
    }
  }, focusFirstError(form, WORKSHOP_FIELD_NAMES))

  const handleLoadLatest = async () => {
    const loaded = await latest.load(async () => {
      const record = await onLoadLatest()
      return record && valuesOf(record)
    })
    if (loaded) serverErrors.clear()
  }

  return (
    <Form {...form}>
      <form onSubmit={handleSubmit} noValidate aria-label="Edit workshop" className="flex max-w-lg flex-col gap-5">
        <WorkshopFields limits={limits} zone={zone} />
        <RefusedSaveAlert problem={serverErrors.problem} />
        <StaleVersionAlert
          noun="workshop"
          problem={serverErrors.problem}
          isLoadingLatest={latest.isLoading}
          onLoadLatest={handleLoadLatest}
        />
        <LatestLoadedNote noun="workshop" changed={latest.changed} labels={FIELD_LABELS} />
        <Button type="submit" className="self-start" isPending={form.formState.isSubmitting}>
          Save
        </Button>
      </form>
    </Form>
  )
}
