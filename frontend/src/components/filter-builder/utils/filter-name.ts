import { toCalendarDate, type CalendarDateTime } from '@internationalized/date'

import { browserTimeZone, formatDate as formatCalendarDate, formatInstant } from '@/lib/zoned-time'

import { isDateValue, isNumberValue, isTextValue, type DateValue, type Filter } from '../types'
import { getOperatorLabel } from './operators'

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
  return `${filter.propertyLabel} | ${getOperatorLabel(filter.operator)} | ${formatFilterValue(filter)}`
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

  if (isDateValue(value)) {
    const options: Intl.DateTimeFormatOptions = { month: 'short', day: 'numeric', year: 'numeric' }

    const formatDate = (date: Date | CalendarDateTime) =>
      date instanceof Date
        ? formatInstant(date, value.timeZone ?? browserTimeZone(), options)
        : formatCalendarDate(toCalendarDate(date), options)

    switch (value.mode) {
      case 'before':
        return `Before ${formatDate(value.startDate)}`
      case 'after':
        return `After ${formatDate(value.startDate)}`
      case 'on':
        return formatDate(value.startDate)
      case 'excluding':
        return `Excluding ${formatDate(value.startDate)}`
      case 'between':
        if (value.endDate) {
          const range = `${formatDate(value.startDate)} - ${formatDate(value.endDate)}`

          return value.exclude ? `Excluding ${range}` : range
        }

        return `From ${formatDate(value.startDate)}`
      default:
        return 'Unknown date'
    }
  }

  if (value === true) return 'True'

  if (value === false) return 'False'

  if (isNumberValue(value)) return value.toString()

  if (isTextValue(value)) return value

  return 'Unknown'
}
