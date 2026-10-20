"use client"

import { Calendar03Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"
import {
  DatePicker as AriaDatePicker,
  DateRangePicker as AriaDateRangePicker,
  Dialog,
  Group,
  composeRenderProps,
  Text,
  type DatePickerProps as AriaDatePickerProps,
  type DateRangePickerProps as AriaDateRangePickerProps,
  type DateValue as AriaDateValue,
  type ValidationResult as AriaValidationResult,
} from "react-aria-components"

import { cn } from "@/lib/utils"

import { Button } from "@/components/ui/button"
import {
  Calendar,
  CalendarCell,
  CalendarGrid,
  CalendarGridBody,
  CalendarGridHeader,
  CalendarHeaderCell,
  CalendarHeading,
  RangeCalendar,
} from "@/components/ui/calendar"
import { DateInput } from "@/components/ui/datefield"
import { Label } from "@/components/ui/label"
import { Popover } from "@/components/ui/popover"

const DatePicker = AriaDatePicker

const DateRangePicker = AriaDateRangePicker

interface JollyDatePickerProps<T extends AriaDateValue>
  extends AriaDatePickerProps<T> {
  label?: string
  description?: string
  errorMessage?: string | ((validation: AriaValidationResult) => string)
}

function JollyDatePicker<T extends AriaDateValue>({
  label,
  description,
  errorMessage,
  className,
  ...props
}: JollyDatePickerProps<T>) {
  return (
    <DatePicker
      className={composeRenderProps(className, (className) =>
        cn("group flex flex-col gap-2", className)
      )}
      {...props}
    >
      {label && <Label>{label}</Label>}
      <Group className="relative flex h-8 w-full items-center overflow-hidden rounded-lg border border-input bg-transparent px-2.5 py-1 text-sm transition-colors outline-none data-focus-within:border-ring data-focus-within:ring-3 data-focus-within:ring-ring/50 data-disabled:opacity-50 dark:bg-input/30">
        <DateInput className="flex-1" variant="ghost" />
        <Button
          variant="ghost"
          size="icon"
          className="size-6 data-[focus-visible]:ring-offset-0"
        >
          <HugeiconsIcon icon={Calendar03Icon} aria-hidden className="size-4" />
        </Button>
      </Group>
      <Popover className="w-auto p-3">
        <Dialog className="outline-none">
          <Calendar>
            <CalendarHeading />
            <CalendarGrid>
              <CalendarGridHeader>
                {(day) => <CalendarHeaderCell>{day}</CalendarHeaderCell>}
              </CalendarGridHeader>
              <CalendarGridBody>
                {(date) => <CalendarCell date={date} />}
              </CalendarGridBody>
            </CalendarGrid>
          </Calendar>
        </Dialog>
      </Popover>
      {description && (
        <Text className="text-sm text-muted-foreground" slot="description">
          {description}
        </Text>
      )}
      {errorMessage && (
        <Text className="text-sm text-destructive" slot="errorMessage">
          {typeof errorMessage === "function" ? "" : errorMessage}
        </Text>
      )}
    </DatePicker>
  )
}

interface JollyDateRangePickerProps<T extends AriaDateValue>
  extends AriaDateRangePickerProps<T> {
  label?: string
  description?: string
  errorMessage?: string | ((validation: AriaValidationResult) => string)
}

function JollyDateRangePicker<T extends AriaDateValue>({
  label,
  description,
  errorMessage,
  className,
  ...props
}: JollyDateRangePickerProps<T>) {
  return (
    <DateRangePicker
      className={composeRenderProps(className, (className) =>
        cn("group flex flex-col gap-2", className)
      )}
      {...props}
    >
      {label && <Label>{label}</Label>}
      <Group className="relative flex h-8 w-full items-center overflow-hidden rounded-lg border border-input bg-transparent px-2.5 py-1 text-sm transition-colors outline-none data-focus-within:border-ring data-focus-within:ring-3 data-focus-within:ring-ring/50 data-disabled:opacity-50 dark:bg-input/30">
        <DateInput variant="ghost" slot="start" />
        <span aria-hidden className="px-2 text-sm text-muted-foreground">
          -
        </span>
        <DateInput className="flex-1" variant="ghost" slot="end" />
        <Button
          variant="ghost"
          size="icon"
          className="size-6 data-[focus-visible]:ring-offset-0"
        >
          <HugeiconsIcon icon={Calendar03Icon} aria-hidden className="size-4" />
        </Button>
      </Group>
      <Popover className="w-auto p-3">
        <Dialog className="outline-none">
          <RangeCalendar>
            <CalendarHeading />
            <CalendarGrid>
              <CalendarGridHeader>
                {(day) => <CalendarHeaderCell>{day}</CalendarHeaderCell>}
              </CalendarGridHeader>
              <CalendarGridBody>
                {(date) => <CalendarCell date={date} />}
              </CalendarGridBody>
            </CalendarGrid>
          </RangeCalendar>
        </Dialog>
      </Popover>
      {description && (
        <Text className="text-sm text-muted-foreground" slot="description">
          {description}
        </Text>
      )}
      {errorMessage && (
        <Text className="text-sm text-destructive" slot="errorMessage">
          {typeof errorMessage === "function" ? "" : errorMessage}
        </Text>
      )}
    </DateRangePicker>
  )
}

export {
  DatePicker,
  DateRangePicker,
  JollyDatePicker,
  JollyDateRangePicker,
}
export type { JollyDatePickerProps, JollyDateRangePickerProps }
