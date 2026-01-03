import { format } from 'date-fns'
import { formatInTimeZone } from 'date-fns-tz'
import type { Filter, FilterGroup, FilterState, DateValue } from '../types'
import { isFilter } from '../types'
import { LogicalOperators } from './operators'

/**
 * Convert filter state to QueryKit filter string
 */
export function toQueryKitString(state: FilterState): string {
  if (state.filters.length === 0) {
    return ''
  }

  if (state.filters.length === 1) {
    return convertFilterOrGroup(state.filters[0])
  }

  const filterStrings = state.filters.map(convertFilterOrGroup)
  const operator = state.rootLogicalOperator === LogicalOperators.AND ? ' && ' : ' || '

  return filterStrings.join(operator)
}

/**
 * Convert a single filter or group to QueryKit string
 */
function convertFilterOrGroup(item: Filter | FilterGroup): string {
  if (isFilter(item)) {
    return convertFilter(item)
  }
  return convertGroup(item)
}

/**
 * Convert a filter group to QueryKit string
 */
function convertGroup(group: FilterGroup): string {
  if (group.filters.length === 0) {
    return ''
  }

  if (group.filters.length === 1) {
    return `(${convertFilterOrGroup(group.filters[0])})`
  }

  const filterStrings = group.filters.map(convertFilterOrGroup)
  const operator = group.logicalOperator === LogicalOperators.AND ? ' && ' : ' || '

  return `(${filterStrings.join(operator)})`
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
  // For text values, wrap in quotes if they contain spaces
  const quotedValue = value.includes(' ') ? `"${value}"` : value
  return `${propertyKey} ${operator} ${quotedValue}`
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
  const formattedValues = value.map((v) => (v.includes(' ') ? `"${v}"` : v)).join(', ')

  // Add % prefix to operator for "all must match" semantics
  const finalOperator = matchAll ? `%${operator}` : operator

  return `${propertyKey} ${finalOperator} [${formattedValues}]`
}

/**
 * Get timezone offset string in ±HH:mm format
 */
function getTimezoneOffset(date: Date): string {
  const offset = -date.getTimezoneOffset()
  const sign = offset >= 0 ? '+' : '-'
  const hours = Math.floor(Math.abs(offset) / 60).toString().padStart(2, '0')
  const minutes = (Math.abs(offset) % 60).toString().padStart(2, '0')
  return `${sign}${hours}:${minutes}`
}

/**
 * Convert a date filter to QueryKit string
 */
function convertDateFilter(propertyKey: string, _operator: string, value: DateValue): string {
  const { mode, startDate, endDate, exclude, dateType = 'date' } = value

  // Format dates as ISO 8601 strings based on dateType:
  // - date: YYYY-MM-DD
  // - datetime: YYYY-MM-DDTHH:mm:ss (local, no timezone)
  // - datetimeUtc: YYYY-MM-DDTHH:mm:ssZ (UTC)
  // - datetimeOffset: YYYY-MM-DDTHH:mm:ss±HH:mm (with timezone offset)
  const formatDateValue = (date: Date): string => {
    switch (dateType) {
      case 'datetime':
        // ISO 8601 local datetime: 2022-07-01T00:00:03
        return format(date, "yyyy-MM-dd'T'HH:mm:ss")

      case 'datetimeUtc':
        // ISO 8601 UTC datetime: 2022-07-01T00:00:03Z
        // Convert local time to UTC
        return formatInTimeZone(date, 'UTC', "yyyy-MM-dd'T'HH:mm:ss'Z'")

      case 'datetimeOffset':
        // ISO 8601 datetime with offset: 2022-07-01T00:00:03+01:00
        return format(date, "yyyy-MM-dd'T'HH:mm:ss") + getTimezoneOffset(date)

      case 'date':
      default:
        // ISO 8601 date-only: 2022-07-01
        return format(date, 'yyyy-MM-dd')
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
