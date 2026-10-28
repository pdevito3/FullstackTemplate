import { browserTimeZone, formatInstant } from '@/lib/zoned-time'

import type { DateValue, Filter } from '../types'
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

  if (typeof value === 'boolean') {
    return value ? 'True' : 'False'
  }

  if (typeof value === 'number') {
    return value.toString()
  }

  if (typeof value === 'string') {
    return value
  }

  // DateValue
  if (typeof value === 'object' && 'mode' in value) {
    const dateValue = value as DateValue
    const zone = dateValue.timeZone ?? browserTimeZone()
    const formatDate = (date: Date) => formatInstant(date, zone, { month: 'short', day: 'numeric', year: 'numeric' })

    switch (dateValue.mode) {
      case 'before':
        return `Before ${formatDate(dateValue.startDate)}`
      case 'after':
        return `After ${formatDate(dateValue.startDate)}`
      case 'on':
        return formatDate(dateValue.startDate)
      case 'excluding':
        return `Excluding ${formatDate(dateValue.startDate)}`
      case 'between':
        if (dateValue.endDate) {
          const range = `${formatDate(dateValue.startDate)} - ${formatDate(dateValue.endDate)}`
          return dateValue.exclude ? `Excluding ${range}` : range
        }
        return `From ${formatDate(dateValue.startDate)}`
      default:
        return 'Unknown date'
    }
  }

  return 'Unknown'
}
