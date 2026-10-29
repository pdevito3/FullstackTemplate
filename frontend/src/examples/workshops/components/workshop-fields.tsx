import { toCalendarDateTime } from '@internationalized/date'
import { useId } from 'react'
import { Controller, useFormContext } from 'react-hook-form'

import { JollyDatePicker } from '@/components/ui/date-picker'
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field'
import { Input } from '@/components/ui/input'
import { timeZoneLabel, todayAt, type TimeZone } from '@/lib/zoned-time'

import type { WorkshopFieldLimits, WorkshopFieldValues } from './workshop-fields-schema'

interface WorkshopFieldsProps {
  limits: WorkshopFieldLimits
  zone: TimeZone | undefined
}

export function WorkshopFields({ limits, zone }: WorkshopFieldsProps) {
  const { control, register, formState } = useFormContext<WorkshopFieldValues>()
  const id = useId()
  const { errors } = formState

  return (
    <>
      <Field data-invalid={!!errors.title || undefined}>
        <FieldLabel htmlFor={`${id}-title`}>Title</FieldLabel>
        <Input
          id={`${id}-title`}
          aria-invalid={!!errors.title || undefined}
          aria-describedby={`${id}-title-hint ${id}-title-error`}
          {...register('title')}
        />
        <FieldDescription id={`${id}-title-hint`}>Up to {limits.titleMaxLength} characters.</FieldDescription>
        <FieldError id={`${id}-title-error`} errors={[errors.title]} />
      </Field>

      <Controller
        control={control}
        name="startsAt"
        render={({ field, fieldState }) => (
          <JollyDatePicker
            label="Starts at"
            description={zone ? `The time in ${timeZoneLabel(zone)}.` : 'The time at the venue that you pick.'}
            granularity="minute"
            timeZone={zone}
            placeholderValue={zone && toCalendarDateTime(todayAt(zone, new Date()))}
            value={field.value ?? null}
            onChange={field.onChange}
            onBlur={field.onBlur}
            isInvalid={fieldState.invalid}
            errorMessage={fieldState.error?.message}
          />
        )}
      />

      <Field data-invalid={!!errors.capacity || undefined}>
        <FieldLabel htmlFor={`${id}-capacity`}>Capacity</FieldLabel>
        <Input
          id={`${id}-capacity`}
          type="number"
          inputMode="numeric"
          max={limits.capacityMaximum}
          aria-invalid={!!errors.capacity || undefined}
          aria-describedby={`${id}-capacity-hint ${id}-capacity-error`}
          {...register('capacity', { valueAsNumber: true })}
        />
        <FieldDescription id={`${id}-capacity-hint`}>Up to {limits.capacityMaximum} seats.</FieldDescription>
        <FieldError id={`${id}-capacity-error`} errors={[errors.capacity]} />
      </Field>
    </>
  )
}
