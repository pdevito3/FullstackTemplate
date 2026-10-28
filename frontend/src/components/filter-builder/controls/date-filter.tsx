import { useState, useMemo } from 'react'
import {
  CalendarDate,
  getLocalTimeZone,
} from '@internationalized/date'
import type { RangeValue, DateValue as AriaDateValue } from 'react-aria-components'
import { Button } from '@/components/ui/button'
import { JollyCalendar, JollyRangeCalendar } from '@/components/ui/calendar'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useIsMobile } from '@/hooks/use-mobile'
import { cn } from '@/lib/utils'
import type { DateValue, Filter, FilterControlProps } from '../types'
import { Operators } from '../utils/operators'
import { FilterControlFooter } from './filter-control-footer'

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

// Helper to extract time string from Date
function getTimeString(date: Date): string {
  const hours = date.getHours().toString().padStart(2, '0')
  const minutes = date.getMinutes().toString().padStart(2, '0')
  return `${hours}:${minutes}`
}

// Helper to apply time to a date
function applyTimeToDate(date: Date, timeStr: string): Date {
  const [hours, minutes] = timeStr.split(':').map(Number)
  const result = new Date(date)
  result.setHours(hours || 0, minutes || 0, 0, 0)
  return result
}

export function DateFilter({
  config,
  surface,
  initialFilter,
  onSubmit,
  autoFocus = false,
}: FilterControlProps) {
  // 'date' for date-only, 'datetime' for date+time
  const { propertyKey, propertyLabel, dateType = 'date' } = config
  const initialDateValue = initialFilter?.value as DateValue | undefined
  // Show time inputs for any datetime variant
  const isDateTime = dateType === 'datetime' || dateType === 'datetimeUtc' || dateType === 'datetimeOffset'
  const isMobile = useIsMobile()

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
  // null, not undefined, so that the calendar is controlled from the first render.
  const [date, setDate] = useState<CalendarDate | null>(initialCalendarDate ?? null)
  const [dateRange, setDateRange] = useState<RangeValue<AriaDateValue> | null>(initialDateRange)
  const [exclude, setExclude] = useState(
    initialDateValue?.mode === 'excluding'
      ? true
      : initialDateValue?.exclude || false
  )
  // Time state for datetime mode
  const [startTime, setStartTime] = useState<string>(
    initialDateValue?.startDate ? getTimeString(initialDateValue.startDate) : '00:00'
  )
  const [endTime, setEndTime] = useState<string>(
    initialDateValue?.endDate ? getTimeString(initialDateValue.endDate) : '23:59'
  )

  const handleSubmit = () => {
    if (mode === 'between' && !dateRange?.start) return
    if (mode !== 'between' && !date) return

    // Get base dates from calendar
    let startDateValue = mode === 'between'
      ? calendarDateToJsDate(dateRange!.start as CalendarDate)
      : calendarDateToJsDate(date!)
    let endDateValue = mode === 'between' && dateRange?.end
      ? calendarDateToJsDate(dateRange.end as CalendarDate)
      : undefined

    // Apply times if in datetime mode
    if (isDateTime) {
      startDateValue = applyTimeToDate(startDateValue, startTime)
      if (endDateValue) {
        endDateValue = applyTimeToDate(endDateValue, endTime)
      }
    }

    const dateValue: DateValue =
      mode === 'between'
        ? {
            mode: 'between',
            startDate: startDateValue,
            endDate: endDateValue,
            exclude: exclude || undefined,
            dateType: dateType,
          }
        : {
            mode: (exclude ? 'excluding' : mode) as
              | 'before'
              | 'after'
              | 'on'
              | 'excluding',
            startDate: startDateValue,
            dateType: dateType,
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

  const isValid = mode === 'between' ? dateRange?.start : date

  return (
    <div className={cn('flex flex-col p-3', isMobile && 'mx-auto w-full max-w-md')}>
      <div className="flex flex-col gap-3">
        {/* Mode selector */}
        <div className="flex gap-1 flex-wrap">
          {modes.map((m) => (
            <Button
              key={m.value}
              variant={mode === m.value ? 'default' : 'outline'}
              size="sm"
              onPress={() => setMode(m.value)}
              className={cn('flex-1 min-w-0', isMobile && 'h-8')}
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

        {/* Calendar with integrated quick selects and date input */}
        {mode === 'between' ? (
          <JollyRangeCalendar
            value={dateRange}
            onChange={setDateRange}
            showQuickSelects
            layout={isMobile ? 'narrow' : 'wide'}
            // Below the md breakpoint the control shows in a bottom sheet, not an anchored popover, and an empty week
            // there pushes Add Filter off a short screen.
            fixedHeight={!isMobile}
            showDateInput
            autoFocus={autoFocus}
          />
        ) : (
          <JollyCalendar
            value={date}
            onChange={setDate}
            showQuickSelects
            layout={isMobile ? 'narrow' : 'wide'}
            fixedHeight={!isMobile}
            showDateInput
            autoFocus={autoFocus}
          />
        )}

        {/* Time inputs for datetime mode */}
        {isDateTime && (
          <div className="flex flex-col gap-2">
            <div className="text-xs font-medium text-muted-foreground">
              Time
            </div>
            <div className="flex gap-2 items-center">
              {mode === 'between' ? (
                <>
                  <div className="flex-1">
                    <Label className="text-xs text-muted-foreground">Start</Label>
                    <Input
                      type="time"
                      value={startTime}
                      onChange={(e) => setStartTime(e.target.value)}
                      className="h-8"
                    />
                  </div>
                  <div className="flex-1">
                    <Label className="text-xs text-muted-foreground">End</Label>
                    <Input
                      type="time"
                      value={endTime}
                      onChange={(e) => setEndTime(e.target.value)}
                      className="h-8"
                    />
                  </div>
                </>
              ) : (
                <Input
                  type="time"
                  value={startTime}
                  onChange={(e) => setStartTime(e.target.value)}
                  className="h-8"
                />
              )}
            </div>
          </div>
        )}
      </div>

      <FilterControlFooter surface={surface}>
        <Button onPress={handleSubmit} isDisabled={!isValid} className="w-full">
          {initialFilter ? 'Update' : 'Add'} Filter
        </Button>
      </FilterControlFooter>
    </div>
  )
}
