// Core filter types for the FilterBuilder component

export type ControlType = 'text' | 'multiselect' | 'date' | 'number' | 'boolean'

export type OperatorType =
  // Equality
  | '==' | '!=' | '==*' | '!=*'
  // Comparison
  | '>' | '<' | '>=' | '<='
  // String
  | '_=' | '!_=' | '_-=' | '!_-=' | '@=' | '!@='
  | '_=*' | '!_=*' | '_-=*' | '!_-=*' | '@=*' | '!@=*'
  // Phonetic
  | '~~' | '!~'
  // Existence
  | '^$' | '!^$' | '^$*' | '!^$*'
  // Collection
  | '^^' | '!^^' | '^^*' | '!^^*'
  // Count
  | '#==' | '#!=' | '#>' | '#<' | '#>=' | '#<='

export type LogicalOperator = 'AND' | 'OR'

// Date format types for QueryKit:
// - 'date': Date only (2022-07-01)
// - 'datetime': Local datetime without timezone (2022-07-01T00:00:03)
// - 'datetimeUtc': DateTime with UTC indicator (2022-07-01T00:00:03Z)
// - 'datetimeOffset': DateTime with timezone offset (2022-07-01T00:00:03+01:00)
export type DateType = 'date' | 'datetime' | 'datetimeUtc' | 'datetimeOffset'

export interface DateValue {
  mode: 'before' | 'after' | 'between' | 'on' | 'excluding'
  startDate: Date
  endDate?: Date // Only for 'between' mode
  exclude?: boolean // When true with 'between' mode, becomes OR logic (outside range)
  dateType?: DateType // 'date' for date-only, 'datetime' for date+time (defaults to 'date')
}

export interface Filter {
  id: string
  propertyKey: string
  propertyLabel: string
  controlType: ControlType
  operator: OperatorType
  value: string | string[] | number | boolean | DateValue
  caseSensitive?: boolean // For text operators
  selectedLabels?: string[] // For multiselect: display labels (separate from values used in QueryKit)
}

export interface FilterGroup {
  id: string
  type: 'group'
  filters: (Filter | FilterGroup)[]
  logicalOperator: LogicalOperator
}

export interface FilterState {
  filters: (Filter | FilterGroup)[]
  rootLogicalOperator: LogicalOperator
}

export interface FilterOption {
  value: string
  label: string
  isNested?: boolean // If true, this option is nested under a group and won't be selected by "Select All"
}

export interface FilterConfig {
  propertyKey: string
  propertyLabel: string
  controlType: ControlType
  operators?: OperatorType[] // Allowed operators for this property (defaults to all for the type)
  options?: FilterOption[] // For multiselect control type
  defaultOperator?: OperatorType // Default operator when adding a filter
  dateType?: DateType // For date control: 'date' for date-only, 'datetime' for date+time (defaults to 'date')
}

export interface FilterPreset {
  label: string
  filter: FilterState
}

export interface FilterBuilderProps {
  filterOptions: FilterConfig[]
  presets?: FilterPreset[]
  onChange?: (state: FilterState) => void
  initialState?: FilterState
  className?: string
}

// Type guard functions
export function isFilter(item: Filter | FilterGroup): item is Filter {
  return !('type' in item)
}

export function isFilterGroup(item: Filter | FilterGroup): item is FilterGroup {
  return 'type' in item && item.type === 'group'
}
