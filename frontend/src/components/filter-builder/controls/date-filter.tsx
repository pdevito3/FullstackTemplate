import { useState, useMemo } from 'react'
import {
  CalendarDate,
  getLocalTimeZone,
  today,
} from '@internationalized/date'
import type { RangeValue, DateValue as AriaDateValue } from 'react-aria-components'
import { Button } from '@/components/ui/button'
import { JollyCalendar, JollyRangeCalendar } from '@/components/ui/calendar'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import type { DateValue, Filter } from '../types'
import { Operators } from '../utils/operators'

interface DateFilterProps {
  propertyKey: string
  propertyLabel: string
  onSubmit: (filter: Omit<Filter, 'id'>) => void
  initialFilter?: Filter
}

type DateMode = 'before' | 'after' | 'between' | 'on'

// Helper to convert JS Date to CalendarDate
function jsDateToCalendarDate(date: Date): CalendarDate {
  return new CalendarDate(
    date.getFullYear(),
    date.getMonth() + 1,
    date.getDate()
  )
}

// Helper to convert CalendarDate to JS Date
function calendarDateToJsDate(date: CalendarDate): Date {
  return date.toDate(getLocalTimeZone())
}

// Helper for date presets
function getPresetDate(daysOffset: number): CalendarDate {
  const todayDate = today(getLocalTimeZone())
  return todayDate.add({ days: daysOffset })
}

function getWeekRange(weeksOffset: number): { start: CalendarDate; end: CalendarDate } {
  const todayDate = today(getLocalTimeZone())
  const targetWeekStart = todayDate.add({ weeks: weeksOffset })
  // Find start of week (Sunday)
  const dayOfWeek = targetWeekStart.toDate(getLocalTimeZone()).getDay()
  const weekStart = targetWeekStart.subtract({ days: dayOfWeek })
  const weekEnd = weekStart.add({ days: 6 })
  return { start: weekStart, end: weekEnd }
}

function getMonthRange(monthsOffset: number): { start: CalendarDate; end: CalendarDate } {
  const todayDate = today(getLocalTimeZone())
  const targetMonth = todayDate.add({ months: monthsOffset })
  const monthStart = new CalendarDate(targetMonth.year, targetMonth.month, 1)
  const monthEnd = monthStart.add({ months: 1 }).subtract({ days: 1 })
  return { start: monthStart, end: monthEnd }
}

function getYearRange(yearsOffset: number): { start: CalendarDate; end: CalendarDate } {
  const todayDate = today(getLocalTimeZone())
  const targetYear = todayDate.year + yearsOffset
  const yearStart = new CalendarDate(targetYear, 1, 1)
  const yearEnd = new CalendarDate(targetYear, 12, 31)
  return { start: yearStart, end: yearEnd }
}

export function DateFilter({
  propertyKey,
  propertyLabel,
  onSubmit,
  initialFilter,
}: DateFilterProps) {
  const initialDateValue = initialFilter?.value as DateValue | undefined

  // Convert initial values to CalendarDate
  const initialCalendarDate = useMemo(() => {
    if (initialDateValue?.startDate) {
      return jsDateToCalendarDate(initialDateValue.startDate)
    }
    return undefined
  }, [initialDateValue])

  const initialDateRange = useMemo((): RangeValue<AriaDateValue> | null => {
    if (initialDateValue?.mode === 'between' && initialDateValue.endDate) {
      return {
        start: jsDateToCalendarDate(initialDateValue.startDate),
        end: jsDateToCalendarDate(initialDateValue.endDate),
      }
    }
    return null
  }, [initialDateValue])

  const [mode, setMode] = useState<DateMode>(
    initialDateValue?.mode === 'excluding'
      ? 'on'
      : initialDateValue?.mode || 'on'
  )
  const [date, setDate] = useState<CalendarDate | undefined>(initialCalendarDate)
  const [dateRange, setDateRange] = useState<RangeValue<AriaDateValue> | null>(initialDateRange)
  const [exclude, setExclude] = useState(
    initialDateValue?.mode === 'excluding'
      ? true
      : initialDateValue?.exclude || false
  )

  const handlePreset = (preset: () => CalendarDate | { start: CalendarDate; end: CalendarDate }) => {
    const result = preset()
    if (result instanceof CalendarDate) {
      setDate(result)
    } else if ('start' in result) {
      setDateRange({ start: result.start, end: result.end })
      if (mode !== 'between') {
        setMode('between')
      }
    }
  }

  const handleSubmit = () => {
    if (mode === 'between' && !dateRange?.start) return
    if (mode !== 'between' && !date) return

    const dateValue: DateValue =
      mode === 'between'
        ? {
            mode: 'between',
            startDate: calendarDateToJsDate(dateRange!.start as CalendarDate),
            endDate: dateRange?.end ? calendarDateToJsDate(dateRange.end as CalendarDate) : undefined,
            exclude: exclude || undefined,
          }
        : {
            mode: (exclude ? 'excluding' : mode) as
              | 'before'
              | 'after'
              | 'on'
              | 'excluding',
            startDate: calendarDateToJsDate(date!),
          }

    onSubmit({
      propertyKey,
      propertyLabel,
      controlType: 'date',
      operator: Operators.EQUALS,
      value: dateValue,
    } as Omit<Filter, 'id'>)
  }

  const modes: { value: DateMode; label: string }[] = [
    { value: 'on', label: 'On' },
    { value: 'before', label: 'Before' },
    { value: 'after', label: 'After' },
    { value: 'between', label: 'Between' },
  ]

  // Presets for single dates (on, before, after)
  const singleDatePresets = [
    { label: 'Today', action: () => getPresetDate(0) },
    { label: 'Tomorrow', action: () => getPresetDate(1) },
    { label: 'Yesterday', action: () => getPresetDate(-1) },
  ]

  // Presets for date ranges (between)
  const dateRangePresets = [
    { label: 'This Week', action: () => getWeekRange(0) },
    { label: 'Last Week', action: () => getWeekRange(-1) },
    { label: 'Next Week', action: () => getWeekRange(1) },
    { label: 'This Month', action: () => getMonthRange(0) },
    { label: 'Last Month', action: () => getMonthRange(-1) },
    { label: 'Next Month', action: () => getMonthRange(1) },
    { label: 'This Year', action: () => getYearRange(0) },
    { label: 'Last Year', action: () => getYearRange(-1) },
  ]

  const currentPresets =
    mode === 'between' ? dateRangePresets : singleDatePresets

  const isValid = mode === 'between' ? dateRange?.start : date

  return (
    <div className="w-80 p-3 space-y-3">
      {/* Mode selector */}
      <div className="flex gap-1 flex-wrap">
        {modes.map((m) => (
          <Button
            key={m.value}
            variant={mode === m.value ? 'default' : 'outline'}
            size="sm"
            onClick={() => setMode(m.value)}
            className="flex-1 min-w-0"
          >
            {m.label}
          </Button>
        ))}
      </div>

      {/* Exclude checkbox */}
      <Checkbox
        isSelected={exclude}
        onChange={setExclude}
      >
        <Label className="text-sm font-medium leading-none">
          Exclude {mode === 'between' ? 'date range' : 'dates'}
        </Label>
      </Checkbox>

      {/* Quick presets */}
      <div className="space-y-2">
        <div className="text-xs font-medium text-muted-foreground">
          Quick Select
        </div>
        <div className="grid grid-cols-3 gap-1">
          {currentPresets.map((preset) => (
            <Button
              key={preset.label}
              variant="outline"
              size="xs"
              onClick={() => handlePreset(preset.action)}
              className="text-xs h-7"
            >
              {preset.label}
            </Button>
          ))}
        </div>
      </div>

      {/* Calendar */}
      <div className="flex justify-center">
        {mode === 'between' ? (
          <JollyRangeCalendar
            value={dateRange}
            onChange={setDateRange}
          />
        ) : (
          <JollyCalendar
            value={date}
            onChange={setDate}
          />
        )}
      </div>

      {/* Submit button */}
      <Button onClick={handleSubmit} disabled={!isValid} className="w-full">
        {initialFilter ? 'Update' : 'Add'} Filter
      </Button>
    </div>
  )
}
