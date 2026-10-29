import { CalendarDateTime } from '@internationalized/date'
import * as z from 'zod'

import { workshopForCreationDtoSchema } from '@/api/generated/models/zod/workshop-for-creation-dto'
import { workshopForUpdateDtoSchema } from '@/api/generated/models/zod/workshop-for-update-dto'
import type { WorkshopForUpdateDto } from '@/api/generated/models/types/workshop-for-update-dto'
import { maximumOf, maxLengthOf, minimumOf, stringWithMaxLengthOf } from '@/lib/api-contract'
import { instantAt, timeZoneLabel, type TimeZone } from '@/lib/zoned-time'

export type WorkshopFieldsContract =
  | typeof workshopForCreationDtoSchema.shape
  | typeof workshopForUpdateDtoSchema.shape

export interface WorkshopFieldLimits {
  titleMaxLength: number
  capacityMinimum: number
  capacityMaximum: number
}

export function limitsOf(contract: WorkshopFieldsContract): WorkshopFieldLimits {
  return {
    titleMaxLength: maxLengthOf(contract.title),
    capacityMinimum: minimumOf(contract.capacity),
    capacityMaximum: maximumOf(contract.capacity),
  }
}

export function workshopFieldsSchema(contract: WorkshopFieldsContract) {
  const { capacityMinimum, capacityMaximum } = limitsOf(contract)
  return z.object({
    title: stringWithMaxLengthOf(contract.title).min(1, 'Enter a title.'),
    startsAt: z.custom<CalendarDateTime>((value) => value instanceof CalendarDateTime, 'Enter a start.'),
    capacity: z
      .number({ error: 'Enter a number.' })
      .int('Enter a whole number.')
      .min(capacityMinimum, `Use ${capacityMinimum} or more.`)
      .max(capacityMaximum, `Use ${capacityMaximum} or fewer.`),
  })
}

export type WorkshopFieldValues = z.infer<ReturnType<typeof workshopFieldsSchema>>

export const WORKSHOP_FIELD_NAMES = ['title', 'startsAt', 'capacity'] as const

export function toWorkshopBody(
  { title, startsAt, capacity }: WorkshopFieldValues,
  zone: TimeZone,
): Pick<WorkshopForUpdateDto, 'title' | 'startsAt' | 'capacity'> | null {
  const instant = instantAt(startsAt, zone)
  return instant && { title, startsAt: instant.toISOString(), capacity }
}

export function startInGapMessage(zone: TimeZone): string {
  return `This time does not exist in ${timeZoneLabel(zone)} on that day, because the clocks go forward. Choose another time.`
}
