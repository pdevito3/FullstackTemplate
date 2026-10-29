import { toCalendarDate } from '@internationalized/date'

import { offsetAt, wallClockAt } from '@/lib/zoned-time'

import type { Filter, FilterGroup, FilterState, DateValue } from '../types'
import { isFilter } from '../types'
import { wallClockZone } from './date-zone'
import { filterValueFault } from './querykit-text'
import { LogicalOperators } from './operators'

/**
 * Convert filter state to QueryKit filter string. A filter with a value that QueryKit cannot hold is left out, and its
 * badge says so.
 */
export function toQueryKitString(state: FilterState): string {
  return joinFilters(state.filters, state.rootLogicalOperator)
}

function joinFilters(items: (Filter | FilterGroup)[], logicalOperator: FilterGroup['logicalOperator']): string {
  const operator = logicalOperator === LogicalOperators.AND ? ' && ' : ' || '
  return items.map(convertFilterOrGroup).filter(Boolean).join(operator)
}

function convertFilterOrGroup(item: Filter | FilterGroup): string {
  if (isFilter(item)) {
    return filterValueFault(item) ? '' : convertFilter(item)
  }
  const filters = joinFilters(item.filters, item.logicalOperator)
  return filters ? `(${filters})` : ''
}

/**
 * Convert a single filter to QueryKit string
 */
function convertFilter(filter: Filter): string {
  const { propertyKey, operator, value, controlType, matchAll } = filter

  switch (controlType) {
    case 'text':
      return convertTextFilter(propertyKey, operator, value as string)

    case 'multiselect':
      return convertMultiSelectFilter(propertyKey, operator, value as string[], matchAll)

    case 'date':
      return convertDateFilter(propertyKey, operator, value as DateValue)

    case 'number':
      return convertNumberFilter(propertyKey, operator, value as number)

    case 'boolean':
      return convertBooleanFilter(propertyKey, operator, value as boolean)

    default:
      return ''
  }
}

/**
 * Convert a text filter to QueryKit string
 */
function convertTextFilter(propertyKey: string, operator: string, value: string): string {
  return `${propertyKey} ${operator} ${quoteText(value)}`
}

/**
 * QueryKit reads an unquoted value as a name, a number, or `null`, and it has no escape in `"..."`. So a value with a
 * quote goes in a raw literal: the value between runs of quotes that are longer than each run in the value, and at
 * least three long.
 */
function quoteText(value: string): string {
  if (!value.includes('"')) {
    return `"${value}"`
  }
  const longestRun = Math.max(...(value.match(/"+/g) ?? []).map((run) => run.length))
  const delimiter = '"'.repeat(Math.max(3, longestRun + 1))
  return `${delimiter}${value}${delimiter}`
}

/**
 * Convert a multiselect filter to QueryKit string
 */
function convertMultiSelectFilter(
  propertyKey: string,
  operator: string,
  value: string[],
  matchAll?: boolean
): string {
  if (value.length === 0) {
    return ''
  }

  // Format array values
  const formattedValues = value.map(quoteText).join(', ')

  // Add % prefix to operator for "all must match" semantics
  const finalOperator = matchAll ? `%${operator}` : operator

  return `${propertyKey} ${finalOperator} [${formattedValues}]`
}

/**
 * Convert a date filter to QueryKit string
 */
function convertDateFilter(propertyKey: string, _operator: string, value: DateValue): string {
  const { mode, startDate, endDate, exclude, dateType = 'date' } = value
  const timeZone = wallClockZone(dateType, value.timeZone)

  // Format dates as ISO 8601 strings based on dateType. The date and the wall clock time are those of the zone that the
  // dates were picked in:
  // - date: YYYY-MM-DD
  // - datetime: YYYY-MM-DDTHH:mm:ss (wall clock time, no zone)
  // - datetimeUtc: YYYY-MM-DDTHH:mm:ssZ (UTC)
  // - datetimeOffset: YYYY-MM-DDTHH:mm:ss±HH:mm (wall clock time and its offset)
  const formatDateValue = (date: Date): string => {
    const wallClock = wallClockAt(date, timeZone).set({ millisecond: 0 })
    switch (dateType) {
      case 'datetime':
        return wallClock.toString()

      case 'datetimeUtc':
        return `${date.toISOString().slice(0, 19)}Z`

      case 'datetimeOffset':
        return wallClock.toString() + offsetAt(date, timeZone)

      case 'date':
      default:
        return toCalendarDate(wallClock).toString()
    }
  }

  switch (mode) {
    case 'before':
      return `${propertyKey} < "${formatDateValue(startDate)}"`

    case 'after':
      return `${propertyKey} > "${formatDateValue(startDate)}"`

    case 'on':
      return `${propertyKey} == "${formatDateValue(startDate)}"`

    case 'excluding':
      return `${propertyKey} != "${formatDateValue(startDate)}"`

    case 'between':
      if (!endDate) {
        // Single date still uses parentheses for consistency
        return `(${propertyKey} >= "${formatDateValue(startDate)}")`
      }
      // If exclude is true, use OR logic (dates outside the range)
      if (exclude) {
        return `(${propertyKey} < "${formatDateValue(startDate)}" || ${propertyKey} > "${formatDateValue(endDate)}")`
      }
      // Normal between uses AND logic (dates inside the range) - always wrap in parentheses for proper precedence
      return `(${propertyKey} >= "${formatDateValue(startDate)}" && ${propertyKey} <= "${formatDateValue(endDate)}")`

    default:
      return ''
  }
}

/**
 * Convert a number filter to QueryKit string
 */
function convertNumberFilter(propertyKey: string, operator: string, value: number): string {
  return `${propertyKey} ${operator} ${value}`
}

/**
 * Convert a boolean filter to QueryKit string
 */
function convertBooleanFilter(propertyKey: string, operator: string, value: boolean): string {
  return `${propertyKey} ${operator} ${value}`
}

/**
 * Validate that a filter state can be converted to QueryKit
 */
export function validateFilterState(state: FilterState): {
  valid: boolean
  errors: string[]
} {
  const errors: string[] = []

  function validateItem(item: Filter | FilterGroup, path: string = 'root'): void {
    if (isFilter(item)) {
      // Validate filter has required fields
      if (!item.propertyKey) {
        errors.push(`${path}: Filter missing propertyKey`)
      }
      if (!item.operator) {
        errors.push(`${path}: Filter missing operator`)
      }
      if (item.value === undefined || item.value === null) {
        errors.push(`${path}: Filter missing value`)
      }

      // Validate based on control type
      if (item.controlType === 'multiselect' && Array.isArray(item.value)) {
        if (item.value.length === 0) {
          errors.push(`${path}: Multiselect filter has no selected values`)
        }
      }

      if (item.controlType === 'date' && typeof item.value === 'object') {
        const dateValue = item.value as DateValue
        if (!dateValue.startDate) {
          errors.push(`${path}: Date filter missing startDate`)
        }
        if (dateValue.mode === 'between' && !dateValue.endDate) {
          errors.push(`${path}: Date filter with 'between' mode missing endDate`)
        }
      }
    } else {
      // Validate group
      if (item.filters.length === 0) {
        errors.push(`${path}: Group has no filters`)
      }

      item.filters.forEach((child, index) => {
        validateItem(child, `${path}.filters[${index}]`)
      })
    }
  }

  state.filters.forEach((item, index) => {
    validateItem(item, `filters[${index}]`)
  })

  return {
    valid: errors.length === 0,
    errors,
  }
}
