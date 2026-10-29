import { ZonedDateTime } from '@internationalized/date'
import * as z from 'zod'

import { workshopForCreationDtoSchema } from '@/api/generated/models/zod/workshop-for-creation-dto'
import { workshopForUpdateDtoSchema } from '@/api/generated/models/zod/workshop-for-update-dto'
import type { WorkshopForUpdateDto } from '@/api/generated/models/types/workshop-for-update-dto'
import { maximumOf, maxLengthOf, stringWithMaxLengthOf } from '@/lib/api-contract'

/** The contract of the fields that the create and the edit form share. Each form passes the schema of its own DTO. */
export type WorkshopFieldsContract =
  | typeof workshopForCreationDtoSchema.shape
  | typeof workshopForUpdateDtoSchema.shape

export interface WorkshopFieldLimits {
  titleMaxLength: number
  capacityMaximum: number
}

export function limitsOf(contract: WorkshopFieldsContract): WorkshopFieldLimits {
  return { titleMaxLength: maxLengthOf(contract.title), capacityMaximum: maximumOf(contract.capacity) }
}

/** The form schema of the shared fields. The limits come from the contract, and the Server decides the other rules. */
export function workshopFieldsSchema(contract: WorkshopFieldsContract) {
  const { capacityMaximum } = limitsOf(contract)
  return z.object({
    title: stringWithMaxLengthOf(contract.title).min(1, 'Enter a title.'),
    startsAt: z.custom<ZonedDateTime>((value) => value instanceof ZonedDateTime, 'Enter a start.'),
    capacity: z
      .number({ error: 'Enter a number.' })
      .int('Enter a whole number.')
      .max(capacityMaximum, `Use ${capacityMaximum} or fewer.`),
  })
}

export type WorkshopFieldValues = z.infer<ReturnType<typeof workshopFieldsSchema>>

export const WORKSHOP_FIELD_NAMES = ['title', 'startsAt', 'capacity'] as const

/** The body fields of the shared values. The start goes to the Server as an instant, which it keeps with its offset. */
export function toWorkshopBody({ title, startsAt, capacity }: WorkshopFieldValues): Pick<WorkshopForUpdateDto, 'title' | 'startsAt' | 'capacity'> {
  return { title, startsAt: startsAt.toAbsoluteString(), capacity }
}
