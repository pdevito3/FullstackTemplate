import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { parseAbsoluteToLocal } from '@internationalized/date'
import { useForm } from 'react-hook-form'

import type { WorkshopDto } from '@/api/generated/models/types/workshop-dto'
import type { WorkshopForUpdateDto } from '@/api/generated/models/types/workshop-for-update-dto'
import { workshopForUpdateDtoSchema } from '@/api/generated/models/zod/workshop-for-update-dto'
import { Button } from '@/components/ui/button'
import { Form } from '@/components/ui/form'
import { RefusedSaveAlert } from '@/examples/components/refused-save-alert'
import { useServerErrors } from '@/examples/components/use-server-errors'

import { StaleVersionAlert } from './stale-version-alert'
import { WorkshopFields } from './workshop-fields'
import { WORKSHOP_FIELD_NAMES, limitsOf, toWorkshopBody, workshopFieldsSchema } from './workshop-fields-schema'

const contract = workshopForUpdateDtoSchema.shape
const schema = workshopFieldsSchema(contract).extend({ version: contract.version })
const limits = limitsOf(contract)

function valuesOf(workshop: WorkshopDto) {
  return {
    title: workshop.title,
    startsAt: parseAbsoluteToLocal(workshop.startsAt),
    capacity: workshop.capacity,
    version: workshop.version,
  }
}

interface WorkshopEditFormProps {
  workshop: WorkshopDto
  onSave: (body: WorkshopForUpdateDto) => Promise<unknown>
  /** Reads the workshop from the Server again. */
  onLoadLatest: () => Promise<WorkshopDto | undefined>
}

/**
 * The edit form keeps the version that it loaded and sends it with the save. It does not take a newer version from a
 * refetch, because the save would then overwrite a change that the user did not see.
 */
export function WorkshopEditForm({ workshop, onSave, onLoadLatest }: WorkshopEditFormProps) {
  const form = useForm({ resolver: zodResolver(schema), defaultValues: valuesOf(workshop) })
  const serverErrors = useServerErrors(form, WORKSHOP_FIELD_NAMES)
  const [isLoadingLatest, setIsLoadingLatest] = useState(false)

  const handleSubmit = form.handleSubmit(async (values) => {
    serverErrors.clear()
    try {
      await onSave({ ...toWorkshopBody(values), version: values.version })
    } catch (error) {
      serverErrors.show(error)
    }
  })

  const handleLoadLatest = async () => {
    setIsLoadingLatest(true)
    try {
      const latest = await onLoadLatest()
      if (latest) {
        form.reset(valuesOf(latest))
        serverErrors.clear()
      }
    } finally {
      setIsLoadingLatest(false)
    }
  }

  return (
    <Form {...form}>
      <form onSubmit={handleSubmit} noValidate aria-label="Edit workshop" className="flex max-w-lg flex-col gap-5">
        <WorkshopFields limits={limits} />
        <RefusedSaveAlert problem={serverErrors.problem} />
        <StaleVersionAlert
          problem={serverErrors.problem}
          isLoadingLatest={isLoadingLatest}
          onLoadLatest={handleLoadLatest}
        />
        <Button type="submit" className="self-start" isPending={form.formState.isSubmitting}>
          Save
        </Button>
      </form>
    </Form>
  )
}
