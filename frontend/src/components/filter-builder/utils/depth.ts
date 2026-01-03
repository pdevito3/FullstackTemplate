import type { Filter, FilterGroup, FilterState } from '../types'
import { isFilterGroup } from '../types'

/**
 * Maximum allowed nesting depth for filter groups
 * 0 = root level
 * 1 = first level group
 * 2 = group inside group
 * 3 = group inside group inside group (max)
 */
export const MAX_NESTING_DEPTH = 3

/**
 * Calculate the depth of a filter or group within the filter tree
 * Returns the depth (0 = root level, 1 = first level group, etc.)
 */
export function calculateDepth(
  state: FilterState,
  targetId: string,
  currentDepth = 0
): number | null {
  for (const item of state.filters) {
    if (item.id === targetId) {
      return currentDepth
    }

    if (isFilterGroup(item)) {
      const depth = calculateDepthRecursive(item.filters, targetId, currentDepth + 1)
      if (depth !== null) {
        return depth
      }
    }
  }

  return null
}

function calculateDepthRecursive(
  filters: (Filter | FilterGroup)[],
  targetId: string,
  currentDepth: number
): number | null {
  for (const item of filters) {
    if (item.id === targetId) {
      return currentDepth
    }

    if (isFilterGroup(item)) {
      const depth = calculateDepthRecursive(item.filters, targetId, currentDepth + 1)
      if (depth !== null) {
        return depth
      }
    }
  }

  return null
}

/**
 * Calculate the maximum depth of the entire filter tree
 */
export function getMaximumDepth(state: FilterState): number {
  return getMaximumDepthRecursive(state.filters, 0)
}

function getMaximumDepthRecursive(
  filters: (Filter | FilterGroup)[],
  currentDepth: number
): number {
  let maxDepth = currentDepth

  for (const item of filters) {
    if (isFilterGroup(item)) {
      const depth = getMaximumDepthRecursive(item.filters, currentDepth + 1)
      maxDepth = Math.max(maxDepth, depth)
    }
  }

  return maxDepth
}

/**
 * Calculate the depth of a group's contents (how deep groups are nested inside it)
 */
export function getGroupContentDepth(group: FilterGroup): number {
  return getGroupContentDepthRecursive(group.filters, 0)
}

function getGroupContentDepthRecursive(
  filters: (Filter | FilterGroup)[],
  currentDepth: number
): number {
  let maxDepth = currentDepth

  for (const item of filters) {
    if (isFilterGroup(item)) {
      const depth = getGroupContentDepthRecursive(item.filters, currentDepth + 1)
      maxDepth = Math.max(maxDepth, depth)
    }
  }

  return maxDepth
}

/**
 * Check if a group can accept new nested groups based on max depth constraint
 * @param state - Current filter state
 * @param groupId - ID of the group to check (null = root level)
 * @param itemsToAdd - Items that would be added to the group
 */
export function canAddToGroup(
  state: FilterState,
  groupId: string | null,
  itemsToAdd: (Filter | FilterGroup)[]
): boolean {
  // Calculate current depth of the target group
  const targetDepth = groupId === null ? 0 : calculateDepth(state, groupId)
  if (targetDepth === null) return false

  // Calculate the maximum depth of items being added
  const maxItemDepth = Math.max(
    0,
    ...itemsToAdd
      .filter(isFilterGroup)
      .map(group => getGroupContentDepth(group))
  )

  // Check if adding these items would exceed max depth
  // targetDepth + 1 (new group level) + maxItemDepth (depth of items)
  const resultingDepth = targetDepth + 1 + maxItemDepth

  return resultingDepth <= MAX_NESTING_DEPTH
}

/**
 * Check if creating a new group from selected filters would exceed max depth
 * @param state - Current filter state
 * @param filterIds - IDs of filters to group
 * @param _parentGroupId - ID of parent group (null = root level)
 */
export function canCreateGroup(
  state: FilterState,
  filterIds: string[],
  _parentGroupId: string | null = null
): { canCreate: boolean; reason?: string } {
  if (filterIds.length < 2) {
    return { canCreate: false, reason: 'Need at least 2 filters to create a group' }
  }

  // Find all the items being grouped
  const items = filterIds
    .map(id => findItemById(state, id))
    .filter(Boolean) as (Filter | FilterGroup)[]

  if (items.length !== filterIds.length) {
    return { canCreate: false, reason: 'Some filters not found' }
  }

  // Check if all items are at the same level
  const depths = filterIds.map(id => calculateDepth(state, id))
  const uniqueDepths = new Set(depths)

  if (uniqueDepths.size > 1) {
    return { canCreate: false, reason: 'Cannot group filters from different nesting levels' }
  }

  const currentDepth = depths[0]
  if (currentDepth === null) {
    return { canCreate: false, reason: 'Could not determine filter depth' }
  }

  // Calculate max depth of items being grouped
  const maxItemDepth = Math.max(
    0,
    ...items.filter(isFilterGroup).map(group => getGroupContentDepth(group))
  )

  // New group would be at currentDepth, with content depth of maxItemDepth
  const resultingDepth = currentDepth + maxItemDepth

  if (resultingDepth > MAX_NESTING_DEPTH) {
    return {
      canCreate: false,
      reason: `Creating this group would exceed maximum nesting depth of ${MAX_NESTING_DEPTH}`,
    }
  }

  return { canCreate: true }
}

/**
 * Find an item (filter or group) by ID in the filter tree
 */
export function findItemById(
  state: FilterState,
  targetId: string
): Filter | FilterGroup | null {
  for (const item of state.filters) {
    if (item.id === targetId) {
      return item
    }

    if (isFilterGroup(item)) {
      const found = findItemByIdRecursive(item.filters, targetId)
      if (found) return found
    }
  }

  return null
}

function findItemByIdRecursive(
  filters: (Filter | FilterGroup)[],
  targetId: string
): Filter | FilterGroup | null {
  for (const item of filters) {
    if (item.id === targetId) {
      return item
    }

    if (isFilterGroup(item)) {
      const found = findItemByIdRecursive(item.filters, targetId)
      if (found) return found
    }
  }

  return null
}

/**
 * Get all items at a specific depth
 */
export function getItemsAtDepth(state: FilterState, targetDepth: number): (Filter | FilterGroup)[] {
  const items: (Filter | FilterGroup)[] = []

  if (targetDepth === 0) {
    return state.filters
  }

  function collectAtDepth(
    filters: (Filter | FilterGroup)[],
    currentDepth: number
  ): void {
    for (const item of filters) {
      if (currentDepth === targetDepth) {
        items.push(item)
      }

      if (isFilterGroup(item) && currentDepth < targetDepth) {
        collectAtDepth(item.filters, currentDepth + 1)
      }
    }
  }

  collectAtDepth(state.filters, 0)
  return items
}
