import type { Filter, FilterGroup, FilterState, LogicalOperator } from '../types'
import { isFilter, isFilterGroup } from '../types'
import { LogicalOperators } from './operators'

/**
 * Add a filter to the root level of the state
 */
export function addFilter(state: FilterState, filter: Filter): FilterState {
  return {
    ...state,
    filters: [...state.filters, filter],
  }
}

/**
 * Add a filter to a specific group
 */
export function addFilterToGroup(
  state: FilterState,
  groupId: string,
  filter: Filter
): FilterState {
  return {
    ...state,
    filters: state.filters.map((item) => {
      if (isFilterGroup(item) && item.id === groupId) {
        return {
          ...item,
          filters: [...item.filters, filter],
        }
      }
      if (isFilterGroup(item)) {
        return addFilterToGroupRecursive(item, groupId, filter)
      }
      return item
    }),
  }
}

function addFilterToGroupRecursive(
  group: FilterGroup,
  groupId: string,
  filter: Filter
): FilterGroup {
  return {
    ...group,
    filters: group.filters.map((item) => {
      if (isFilterGroup(item) && item.id === groupId) {
        return {
          ...item,
          filters: [...item.filters, filter],
        }
      }
      if (isFilterGroup(item)) {
        return addFilterToGroupRecursive(item, groupId, filter)
      }
      return item
    }),
  }
}

/**
 * Remove a filter by ID
 */
export function removeFilter(state: FilterState, filterId: string): FilterState {
  return {
    ...state,
    filters: removeFilterRecursive(state.filters, filterId),
  }
}

function removeFilterRecursive(
  filters: (Filter | FilterGroup)[],
  filterId: string
): (Filter | FilterGroup)[] {
  return filters
    .filter((item) => item.id !== filterId)
    .map((item) => {
      if (isFilterGroup(item)) {
        return {
          ...item,
          filters: removeFilterRecursive(item.filters, filterId),
        }
      }
      return item
    })
}

/**
 * Update a filter by ID
 */
export function updateFilter(
  state: FilterState,
  filterId: string,
  updates: Partial<Filter>
): FilterState {
  return {
    ...state,
    filters: updateFilterRecursive(state.filters, filterId, updates),
  }
}

function updateFilterRecursive(
  filters: (Filter | FilterGroup)[],
  filterId: string,
  updates: Partial<Filter>
): (Filter | FilterGroup)[] {
  return filters.map((item) => {
    if (isFilter(item) && item.id === filterId) {
      return { ...item, ...updates }
    }
    if (isFilterGroup(item)) {
      return {
        ...item,
        filters: updateFilterRecursive(item.filters, filterId, updates),
      }
    }
    return item
  })
}

/**
 * Create a group from selected filters
 */
export function createGroup(
  state: FilterState,
  filterIds: string[],
  logicalOperator: LogicalOperator = LogicalOperators.AND
): FilterState {
  const filtersToGroup: (Filter | FilterGroup)[] = []
  const remainingFilters: (Filter | FilterGroup)[] = []

  state.filters.forEach((filter) => {
    if (filterIds.includes(filter.id)) {
      filtersToGroup.push(filter)
    } else {
      remainingFilters.push(filter)
    }
  })

  if (filtersToGroup.length === 0) {
    return state
  }

  const newGroup: FilterGroup = {
    id: crypto.randomUUID(),
    type: 'group',
    filters: filtersToGroup,
    logicalOperator,
  }

  return {
    ...state,
    filters: [...remainingFilters, newGroup],
  }
}

/**
 * Ungroup a filter group (move filters to parent level)
 * Supports nested groups
 */
export function ungroupFilters(state: FilterState, groupId: string): FilterState {
  return {
    ...state,
    filters: ungroupFiltersRecursive(state.filters, groupId),
  }
}

function ungroupFiltersRecursive(
  filters: (Filter | FilterGroup)[],
  groupId: string
): (Filter | FilterGroup)[] {
  const newFilters: (Filter | FilterGroup)[] = []

  filters.forEach((item) => {
    if (isFilterGroup(item) && item.id === groupId) {
      // Ungroup: add all child filters to the current level
      newFilters.push(...item.filters)
    } else if (isFilterGroup(item)) {
      // Recursively process nested groups
      newFilters.push({
        ...item,
        filters: ungroupFiltersRecursive(item.filters, groupId),
      })
    } else {
      newFilters.push(item)
    }
  })

  return newFilters
}

/**
 * Create a group from selected filters (supports nested)
 * All selected filters must be at the same level
 */
export function createGroupFromSelected(
  state: FilterState,
  filterIds: string[],
  logicalOperator: LogicalOperator = LogicalOperators.AND
): FilterState {
  if (filterIds.length < 2) {
    return state
  }

  // Try to create group at root level first
  const rootIds = state.filters.map(f => f.id)
  const allAtRoot = filterIds.every(id => rootIds.includes(id))

  if (allAtRoot) {
    // All filters are at root level
    const filtersToGroup: (Filter | FilterGroup)[] = []
    const remainingFilters: (Filter | FilterGroup)[] = []

    state.filters.forEach((filter) => {
      if (filterIds.includes(filter.id)) {
        filtersToGroup.push(filter)
      } else {
        remainingFilters.push(filter)
      }
    })

    const newGroup: FilterGroup = {
      id: crypto.randomUUID(),
      type: 'group',
      filters: filtersToGroup,
      logicalOperator,
    }

    return {
      ...state,
      filters: [...remainingFilters, newGroup],
    }
  }

  // Filters are nested, recursively find and group them
  return {
    ...state,
    filters: createGroupFromSelectedRecursive(state.filters, filterIds, logicalOperator),
  }
}

function createGroupFromSelectedRecursive(
  filters: (Filter | FilterGroup)[],
  filterIds: string[],
  logicalOperator: LogicalOperator
): (Filter | FilterGroup)[] {
  const currentIds = filters.map(f => f.id)
  const allAtCurrentLevel = filterIds.every(id => currentIds.includes(id))

  if (allAtCurrentLevel) {
    // Group filters at this level
    const filtersToGroup: (Filter | FilterGroup)[] = []
    const remainingFilters: (Filter | FilterGroup)[] = []

    filters.forEach((filter) => {
      if (filterIds.includes(filter.id)) {
        filtersToGroup.push(filter)
      } else {
        remainingFilters.push(filter)
      }
    })

    if (filtersToGroup.length >= 2) {
      const newGroup: FilterGroup = {
        id: crypto.randomUUID(),
        type: 'group',
        filters: filtersToGroup,
        logicalOperator,
      }

      return [...remainingFilters, newGroup]
    }
  }

  // Recursively process groups
  return filters.map((item) => {
    if (isFilterGroup(item)) {
      return {
        ...item,
        filters: createGroupFromSelectedRecursive(item.filters, filterIds, logicalOperator),
      }
    }
    return item
  })
}

/**
 * Toggle the logical operator at the root level
 */
export function toggleRootLogicalOperator(state: FilterState): FilterState {
  return {
    ...state,
    rootLogicalOperator: state.rootLogicalOperator === LogicalOperators.AND ? LogicalOperators.OR : LogicalOperators.AND,
  }
}

/**
 * Toggle the logical operator for a specific group
 */
export function toggleGroupLogicalOperator(state: FilterState, groupId: string): FilterState {
  return {
    ...state,
    filters: toggleGroupOperatorRecursive(state.filters, groupId),
  }
}

function toggleGroupOperatorRecursive(
  filters: (Filter | FilterGroup)[],
  groupId: string
): (Filter | FilterGroup)[] {
  return filters.map((item) => {
    if (isFilterGroup(item) && item.id === groupId) {
      return {
        ...item,
        logicalOperator: item.logicalOperator === LogicalOperators.AND ? LogicalOperators.OR : LogicalOperators.AND,
      }
    }
    if (isFilterGroup(item)) {
      return {
        ...item,
        filters: toggleGroupOperatorRecursive(item.filters, groupId),
      }
    }
    return item
  })
}

/**
 * Find a filter by ID (returns null if not found)
 */
export function findFilterById(
  state: FilterState,
  filterId: string
): Filter | FilterGroup | null {
  return findFilterByIdRecursive(state.filters, filterId)
}

function findFilterByIdRecursive(
  filters: (Filter | FilterGroup)[],
  filterId: string
): Filter | FilterGroup | null {
  for (const item of filters) {
    if (item.id === filterId) {
      return item
    }
    if (isFilterGroup(item)) {
      const found = findFilterByIdRecursive(item.filters, filterId)
      if (found) return found
    }
  }
  return null
}

/**
 * Reorder filters at the root level
 */
export function reorderFilters(
  state: FilterState,
  startIndex: number,
  endIndex: number
): FilterState {
  const result = Array.from(state.filters)
  const [removed] = result.splice(startIndex, 1)
  result.splice(endIndex, 0, removed)

  return {
    ...state,
    filters: result,
  }
}

/**
 * Move a filter to a different group
 */
export function moveFilterToGroup(
  state: FilterState,
  filterId: string,
  targetGroupId: string | null
): FilterState {
  // Find the filter
  const filter = findFilterById(state, filterId)
  if (!filter || isFilterGroup(filter)) {
    return state
  }

  // Remove from current location
  let newState = removeFilter(state, filterId)

  // Add to target group (or root if targetGroupId is null)
  if (targetGroupId === null) {
    newState = addFilter(newState, filter as Filter)
  } else {
    newState = addFilterToGroup(newState, targetGroupId, filter as Filter)
  }

  return newState
}

/**
 * Get all filter IDs (flat list, including nested)
 */
export function getAllFilterIds(state: FilterState): string[] {
  return getAllFilterIdsRecursive(state.filters)
}

function getAllFilterIdsRecursive(filters: (Filter | FilterGroup)[]): string[] {
  const ids: string[] = []
  filters.forEach((item) => {
    ids.push(item.id)
    if (isFilterGroup(item)) {
      ids.push(...getAllFilterIdsRecursive(item.filters))
    }
  })
  return ids
}
