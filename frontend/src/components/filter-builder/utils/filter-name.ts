import type { CalendarDateTime } from '@internationalized/date'

import { browserTimeZone, formatInstant, formatWallClock } from '@/lib/zoned-time'

import { isDateValue, isNumberValue, isTextValue, type DateValue, type Filter } from '../types'
import { getOperatorLabel } from './operators'

/**
 * The operator of a filter as the badge shows it. A date filter has no operator of its own: its mode tells what it
 * does, as the QueryKit string reads it.
 */
export function formatFilterOperator(filter: Filter): string {
  return isDateValue(filter.value) ? dateModeLabel(filter.value) : getOperatorLabel(filter.operator)
}

/**
 * The value of a filter as the badge shows it. A multiselect filter shows its selected labels.
 */
export function formatFilterValue(filter: Filter): string {
  const value = filter.controlType === 'multiselect' && filter.selectedLabels
    ? filter.selectedLabels
    : filter.value

  return formatValue(value)
}

/**
 * The visible text of a filter badge, for example "First Name | Contains | Paul".
 * Accessible names use it, because two filters can have the same property.
 */
export function getFilterName(filter: Filter): string {
  return `${filter.propertyLabel} | ${formatFilterOperator(filter)} | ${formatFilterValue(filter)}`
}

function dateModeLabel(value: DateValue): string {
  switch (value.mode) {
    case 'before':
      return 'Before'
    case 'after':
      return 'After'
    case 'on':
      return 'On'
    case 'excluding':
      return 'Not on'
    case 'between':
      if (!value.endDate) return 'On or after'

      return value.exclude ? 'Not between' : 'Between'
  }
}

function formatDates(value: DateValue): string {
  const hasTime = value.dateType !== undefined && value.dateType !== 'date'

  const options: Intl.DateTimeFormatOptions = hasTime
    ? { month: 'short', day: 'numeric', year: 'numeric', hour: 'numeric', minute: '2-digit' }
    : { month: 'short', day: 'numeric', year: 'numeric' }

  // A user can be in another zone than the place, so a time of a place names its zone.
  const formatDate = (date: Date | CalendarDateTime) =>
    date instanceof Date
      ? formatInstant(date, value.timeZone ?? browserTimeZone(), { ...options, showZone: hasTime && Boolean(value.timeZone) })
      : formatWallClock(date, options)

  return value.mode === 'between' && value.endDate
    ? `${formatDate(value.startDate)} - ${formatDate(value.endDate)}`
    : formatDate(value.startDate)
}

function formatValue(
  value: string | string[] | number | boolean | DateValue
): string {
  if (Array.isArray(value)) {
    if (value.length === 0) return 'None'

    if (value.length === 1) return value[0]

    // Show actual values if less than 4
    if (value.length < 4) return value.join(', ')

    return `${value.length} selected`
  }

  if (isDateValue(value)) return formatDates(value)

  if (value === true) return 'True'

  if (value === false) return 'False'

  if (isNumberValue(value)) return value.toString()

  if (isTextValue(value)) return value

  return 'Unknown'
}
