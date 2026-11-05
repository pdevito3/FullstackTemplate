import { useState, useMemo } from 'react'
import { CalendarDate, parseTime, toCalendarDate, toCalendarDateTime, type CalendarDateTime } from '@internationalized/date'
import type { RangeValue, DateValue as AriaDateValue } from 'react-aria-components'
import { Button } from '@/components/ui/button'
import { JollyCalendar, JollyRangeCalendar } from '@/components/ui/calendar'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { useIsMobile } from '@/hooks/use-mobile'
import { cn } from '@/lib/utils'
import {
  browserTimeZone,
  instantAt,
  springGapMessage,
  startOfDayAt,
  timeZoneLabel,
  wallClockAt,
  type TimeZone,
} from '@/lib/zoned-time'
import type { DateValue, Filter, FilterControlProps } from '../types'
import { Operators } from '../utils/operators'
import { FilterControlFooter } from './filter-control-footer'

type DateMode = 'before' | 'after' | 'between' | 'on'

function wallClockOf(value: Date | CalendarDateTime, zone: TimeZone): CalendarDateTime {
  return value instanceof Date ? wallClockAt(value, zone) : value
}

function dateAt(value: Date | CalendarDateTime, zone: TimeZone): CalendarDate {
  return toCalendarDate(wallClockOf(value, zone))
}

function timeAt(value: Date | CalendarDateTime, zone: TimeZone): string {
  return wallClockOf(value, zone).toString().slice(11, 16)
}

function typedWallClock(date: CalendarDate, time: string): CalendarDateTime {
  return toCalendarDateTime(date, parseTime(time || '00:00'))
}

export function DateFilter({
  config,
  surface,
  initialFilter,
  onSubmit,
  autoFocus = false,
}: FilterControlProps) {
  // 'date' for date-only, 'datetime' for date+time
  const { propertyKey, propertyLabel, dateType = 'date', timeZone } = config
  const initialDateValue = initialFilter?.value as DateValue | undefined
  const placeZone = initialDateValue?.timeZone ?? timeZone
  const zone = placeZone ?? browserTimeZone()
  // Show time inputs for any datetime variant
  const isDateTime = dateType === 'datetime' || dateType === 'datetimeUtc' || dateType === 'datetimeOffset'
  const isMobile = useIsMobile()

  // Convert initial values to CalendarDate
  const initialCalendarDate = useMemo(() => {
    if (initialDateValue?.startDate) {
      return dateAt(initialDateValue.startDate, zone)
    }
    return undefined
  }, [initialDateValue, zone])

  const initialDateRange = useMemo((): RangeValue<AriaDateValue> | null => {
    if (initialDateValue?.mode === 'between' && initialDateValue.endDate) {
      return {
        start: dateAt(initialDateValue.startDate, zone),
        end: dateAt(initialDateValue.endDate, zone),
      }
    }
    return null
  }, [initialDateValue, zone])

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
    initialDateValue?.startDate ? timeAt(initialDateValue.startDate, zone) : '00:00'
  )
  const [endTime, setEndTime] = useState<string>(
    initialDateValue?.endDate ? timeAt(initialDateValue.endDate, zone) : '23:59'
  )

  const startCalendarDate = mode === 'between' ? (dateRange?.start as CalendarDate | undefined) : date
  const endCalendarDate = mode === 'between' ? (dateRange?.end as CalendarDate | undefined) : undefined
  const instantOf = (date: CalendarDate, time: string) =>
    isDateTime ? instantAt(typedWallClock(date, time), zone) : startOfDayAt(date, zone)

  // The dates that the filter keeps, or null when no date is picked or a time is in the spring gap of the zone.
  const pickDates = () => {
    if (!startCalendarDate) return null

    const startInstant = instantOf(startCalendarDate, startTime)
    const endInstant = endCalendarDate ? instantOf(endCalendarDate, endTime) : undefined
    const isInGap = !startInstant || endInstant === null

    if (dateType === 'datetime') {
      // A wall clock with no place has no zone, so no time of day is in a gap.
      return placeZone && isInGap
        ? null
        : {
            dateType,
            startDate: typedWallClock(startCalendarDate, startTime),
            endDate: endCalendarDate && typedWallClock(endCalendarDate, endTime),
          }
    }

    return isInGap ? null : { dateType, startDate: startInstant, endDate: endInstant }
  }

  const dates = pickDates()
  const isTimeInGap = Boolean(startCalendarDate) && !dates

  const handleSubmit = () => {
    if (!dates) return

    const dateValue: DateValue =
      mode === 'between'
        ? {
            ...dates,
            mode: 'between',
            exclude: exclude || undefined,
            timeZone: placeZone,
          }
        : {
            ...dates,
            mode: (exclude ? 'excluding' : mode) as
              | 'before'
              | 'after'
              | 'on'
              | 'excluding',
            timeZone: placeZone,
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

  const isValid = Boolean(dates)
  // A user can be in another zone than the place, so the label names the zone of the place.
  const timeLabel = placeZone ? timeZoneLabel(placeZone) : 'Time'
  // In a wide sheet, such as a phone on its side, a month grid of the full width has wide, flat day cells. The other
  // fields and the footer use the full width, as in the other controls.
  const calendarClassName = isMobile ? 'mx-auto w-full max-w-md' : undefined

  return (
    <div className="flex flex-col p-3">
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
            className={calendarClassName}
            value={dateRange}
            onChange={setDateRange}
            showQuickSelects
            layout={isMobile ? 'narrow' : 'wide'}
            // Below the md breakpoint the control shows in a bottom sheet, not an anchored popover, and an empty week
            // there pushes Add Filter off a short screen.
            fixedHeight={!isMobile}
            showDateInput
            autoFocus={autoFocus}
            timeZone={zone}
          />
        ) : (
          <JollyCalendar
            className={calendarClassName}
            value={date}
            onChange={setDate}
            showQuickSelects
            layout={isMobile ? 'narrow' : 'wide'}
            fixedHeight={!isMobile}
            showDateInput
            autoFocus={autoFocus}
            timeZone={zone}
          />
        )}

        {/* Time inputs for datetime mode */}
        {isDateTime && (
          <div className="flex flex-col gap-2">
            <div className="text-xs font-medium text-muted-foreground">
              {timeLabel}
            </div>
            <div className="flex gap-2 items-center">
              {mode === 'between' ? (
                <>
                  <div className="flex-1">
                    <Label className="text-xs text-muted-foreground">Start</Label>
                    <Input
                      type="time"
                      aria-label={`Start, ${timeLabel}`}
                      value={startTime}
                      onChange={(e) => setStartTime(e.target.value)}
                      className="h-8"
                    />
                  </div>
                  <div className="flex-1">
                    <Label className="text-xs text-muted-foreground">End</Label>
                    <Input
                      type="time"
                      aria-label={`End, ${timeLabel}`}
                      value={endTime}
                      onChange={(e) => setEndTime(e.target.value)}
                      className="h-8"
                    />
                  </div>
                </>
              ) : (
                <Input
                  type="time"
                  aria-label={timeLabel}
                  value={startTime}
                  onChange={(e) => setStartTime(e.target.value)}
                  className="h-8"
                />
              )}
            </div>
            {isTimeInGap && (
              <p role="alert" className="text-destructive text-sm">
                {springGapMessage(placeZone)}
              </p>
            )}
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
