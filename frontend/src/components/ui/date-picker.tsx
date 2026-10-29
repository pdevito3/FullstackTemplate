"use client"

import { Calendar03Icon } from "@hugeicons/core-free-icons"
import { HugeiconsIcon } from "@hugeicons/react"
import { useState } from "react"
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
  type RangeValue as AriaRangeValue,
  type ValidationResult as AriaValidationResult,
} from "react-aria-components"

import { cn } from "@/lib/utils"
import { todayAt, type TimeZone } from "@/lib/zoned-time"

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
import { shortYearMessage, useShortYearCheck } from "@/components/ui/use-short-year-check"

const DatePicker = AriaDatePicker

const DateRangePicker = AriaDateRangePicker

interface JollyDatePickerProps<T extends AriaDateValue>
  extends AriaDatePickerProps<T> {
  label?: string
  description?: string
  errorMessage?: string | ((validation: AriaValidationResult) => string)
  /** The zone of the place that the date belongs to. The calendar marks today at that place, and opens on its month. */
  timeZone?: TimeZone
}

function JollyDatePicker<T extends AriaDateValue>({
  label,
  description,
  errorMessage,
  className,
  value,
  defaultValue,
  onChange,
  onBlur,
  isInvalid,
  timeZone,
  ...props
}: JollyDatePickerProps<T>) {
  const [changedDate, setChangedDate] = useState<AriaDateValue | null>(defaultValue ?? null)
  const date = value === undefined ? changedDate : value
  const yearCheck = useShortYearCheck(date)

  const handleChange: JollyDatePickerProps<T>["onChange"] = (newValue) => {
    setChangedDate(newValue)
    onChange?.(newValue)
  }

  // React Aria calls it only when the focus leaves the whole picker. A move to the calendar button or the calendar
  // does not mark a short year.
  const handleBlur: JollyDatePickerProps<T>["onBlur"] = (event) => {
    yearCheck.onBlur()
    onBlur?.(event)
  }

  return (
    <DatePicker
      className={composeRenderProps(className, (className) =>
        cn("group flex flex-col gap-2", className)
      )}
      value={value}
      defaultValue={defaultValue}
      onChange={handleChange}
      onBlur={handleBlur}
      // Undefined when neither is set, so that React Aria keeps its own validation.
      isInvalid={yearCheck.isShortYear || isInvalid}
      {...props}
    >
      {label && <Label>{label}</Label>}
      <Group className="relative flex min-h-8 w-full items-center overflow-hidden rounded-lg border border-input bg-transparent px-2.5 py-0.5 text-sm transition-colors outline-none data-focus-within:border-ring data-focus-within:ring-3 data-focus-within:ring-ring/50 data-invalid:border-destructive data-invalid:ring-3 data-invalid:ring-destructive/20 data-disabled:opacity-50 dark:bg-input/30 dark:data-invalid:border-destructive/50 dark:data-invalid:ring-destructive/40">
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
          <Calendar defaultFocusedValue={date || props.placeholderValue || !timeZone ? undefined : todayAt(timeZone, new Date())}>
            <CalendarHeading />
            <CalendarGrid>
              <CalendarGridHeader>
                {(day) => <CalendarHeaderCell>{day}</CalendarHeaderCell>}
              </CalendarGridHeader>
              <CalendarGridBody>
                {(date) => <CalendarCell date={date} timeZone={timeZone} />}
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
      {yearCheck.isShortYear && (
        <Text className="text-sm text-destructive" slot="errorMessage" role="alert">
          {shortYearMessage}
        </Text>
      )}
      {!yearCheck.isShortYear && errorMessage && (
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
  /** The zone of the place that the date belongs to. The calendar marks today at that place, and opens on its month. */
  timeZone?: TimeZone
}

function JollyDateRangePicker<T extends AriaDateValue>({
  label,
  description,
  errorMessage,
  className,
  value,
  defaultValue,
  onChange,
  onBlur,
  isInvalid,
  timeZone,
  ...props
}: JollyDateRangePickerProps<T>) {
  const [changedRange, setChangedRange] = useState<AriaRangeValue<AriaDateValue> | null>(defaultValue ?? null)
  const range = value === undefined ? changedRange : value
  const startYearCheck = useShortYearCheck(range?.start ?? null)
  const endYearCheck = useShortYearCheck(range?.end ?? null)
  const isShortYear = startYearCheck.isShortYear || endYearCheck.isShortYear

  const handleChange: JollyDateRangePickerProps<T>["onChange"] = (newValue) => {
    setChangedRange(newValue)
    onChange?.(newValue)
  }

  const handleBlur: JollyDateRangePickerProps<T>["onBlur"] = (event) => {
    startYearCheck.onBlur()
    endYearCheck.onBlur()
    onBlur?.(event)
  }

  return (
    <DateRangePicker
      className={composeRenderProps(className, (className) =>
        cn("group flex flex-col gap-2", className)
      )}
      value={value}
      defaultValue={defaultValue}
      onChange={handleChange}
      onBlur={handleBlur}
      isInvalid={isShortYear || isInvalid}
      {...props}
    >
      {label && <Label>{label}</Label>}
      <Group className="relative flex h-8 w-full items-center overflow-hidden rounded-lg border border-input bg-transparent px-2.5 py-1 text-sm transition-colors outline-none data-focus-within:border-ring data-focus-within:ring-3 data-focus-within:ring-ring/50 data-invalid:border-destructive data-invalid:ring-3 data-invalid:ring-destructive/20 data-disabled:opacity-50 dark:bg-input/30 dark:data-invalid:border-destructive/50 dark:data-invalid:ring-destructive/40">
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
          <RangeCalendar defaultFocusedValue={range || props.placeholderValue || !timeZone ? undefined : todayAt(timeZone, new Date())}>
            <CalendarHeading />
            <CalendarGrid>
              <CalendarGridHeader>
                {(day) => <CalendarHeaderCell>{day}</CalendarHeaderCell>}
              </CalendarGridHeader>
              <CalendarGridBody>
                {(date) => <CalendarCell date={date} timeZone={timeZone} />}
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
      {isShortYear && (
        <Text className="text-sm text-destructive" slot="errorMessage" role="alert">
          {shortYearMessage}
        </Text>
      )}
      {!isShortYear && errorMessage && (
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
