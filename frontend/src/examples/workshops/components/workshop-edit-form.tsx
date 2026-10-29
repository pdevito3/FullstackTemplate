import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'

import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import type { WorkshopForUpdateDto } from '@/api/generated/models/types/workshop-for-update-dto'
import { workshopForUpdateDtoSchema } from '@/api/generated/models/zod/workshop-for-update-dto'
import { RefusedSaveAlert } from '@/components/refused-save-alert'
import { Button } from '@/components/ui/button'
import { Form } from '@/components/ui/form'
import { useServerErrors } from '@/hooks/use-server-errors'
import { focusFirstError } from '@/lib/focus-first-error'
import { latestOverEdits } from '@/lib/latest-over-edits'
import { parseTimeZone, wallClockAt } from '@/lib/zoned-time'

import { LatestLoadedNote, StaleVersionAlert } from './stale-version-alert'
import { WorkshopFields } from './workshop-fields'
import {
  WORKSHOP_FIELD_NAMES,
  limitsOf,
  startInGapMessage,
  toWorkshopBody,
  workshopFieldsSchema,
  type WorkshopFieldName,
} from './workshop-fields-schema'

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
  const [isLoadingLatest, setIsLoadingLatest] = useState(false)
  const [changedByLatest, setChangedByLatest] = useState<WorkshopFieldName[] | null>(null)
  const zone = parseTimeZone(workshop.venueTimeZone)

  const handleSubmit = form.handleSubmit(async (values) => {
    serverErrors.clear()
    setChangedByLatest(null)
    const body = toWorkshopBody(values, zone)
    if (!body) {
      form.setError('startsAt', { message: startInGapMessage(zone) }, { shouldFocus: true })
      return
    }
    try {
      await onSave({ ...body, version: values.version })
    } catch (error) {
      serverErrors.show(error)
    }
  }, focusFirstError(form, WORKSHOP_FIELD_NAMES))

  const handleLoadLatest = async () => {
    setIsLoadingLatest(true)
    try {
      const latest = await onLoadLatest()
      if (latest) {
        const edited = form.getValues()
        const latestValues = valuesOf(latest)
        const { changed, kept } = latestOverEdits(form.formState.defaultValues ?? {}, edited, latestValues, WORKSHOP_FIELD_NAMES)
        // Before the reset, because a reset drops the field refs that setFocus reads until the next render.
        form.setFocus(changed[0] ?? WORKSHOP_FIELD_NAMES[0])
        form.reset(latestValues)
        for (const field of kept) form.setValue(field, edited[field], { shouldDirty: true })
        setChangedByLatest(changed)
        serverErrors.clear()
      }
    } finally {
      setIsLoadingLatest(false)
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={handleSubmit} noValidate aria-label="Edit workshop" className="flex max-w-lg flex-col gap-5">
        <WorkshopFields limits={limits} zone={zone} />
        <RefusedSaveAlert problem={serverErrors.problem} />
        <StaleVersionAlert
          problem={serverErrors.problem}
          isLoadingLatest={isLoadingLatest}
          onLoadLatest={handleLoadLatest}
        />
        <LatestLoadedNote changed={changedByLatest} labels={FIELD_LABELS} />
        <Button type="submit" className="self-start" isPending={form.formState.isSubmitting}>
          Save
        </Button>
      </form>
    </Form>
  )
}
