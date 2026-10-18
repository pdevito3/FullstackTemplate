// Main component
export { FilterBuilder } from './filter-builder'
export { toQueryKitString } from './utils/querykit-converter'

// Types
export type {
  Filter,
  FilterGroup,
  FilterState,
  FilterConfig,
  FilterPreset,
  FilterBuilderProps,
  ControlType,
  OperatorType,
  LogicalOperator,
  DateValue,
  FilterOption,
} from './types'

// Utilities
export {
  getOperatorLabel,
  getOperatorsForControlType,
  getDefaultOperator,
  Operators,
  LogicalOperators,
} from './utils/operators'
export {
  addFilter,
  removeFilter,
  updateFilter,
  createGroup,
  createGroupFromSelected,
  ungroupFilters,
  toggleRootLogicalOperator,
  toggleGroupLogicalOperator,
} from './utils/filter-state'
export { validateFilterState } from './utils/querykit-converter'
export {
  calculateDepth,
  canCreateGroup,
  canAddToGroup,
  getMaximumDepth,
  MAX_NESTING_DEPTH,
} from './utils/depth'
