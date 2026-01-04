import { useCallback, useReducer } from 'react'
import type { Filter, FilterState, LogicalOperator } from './types'
import {
  addFilter,
  createGroupFromSelected,
  removeFilter,
  toggleGroupLogicalOperator,
  toggleRootLogicalOperator,
  ungroupFilters,
  updateFilter,
} from './utils/filter-state'
import { LogicalOperators } from './utils/operators'

// Action types (internal)
type FilterBuilderAction =
  | { type: 'ADD_FILTER'; payload: Filter }
  | { type: 'REMOVE_FILTER'; payload: { filterId: string } }
  | { type: 'UPDATE_FILTER'; payload: { filterId: string; updates: Omit<Filter, 'id'> } }
  | { type: 'TOGGLE_ROOT_OPERATOR' }
  | { type: 'TOGGLE_GROUP_OPERATOR'; payload: { groupId: string } }
  | { type: 'UNGROUP'; payload: { groupId: string } }
  | { type: 'CREATE_GROUP'; payload: { filterIds: string[]; operator: LogicalOperator } }
  | { type: 'CLEAR_ALL' }
  | { type: 'APPLY_PRESET'; payload: FilterState }

const initialState: FilterState = {
  filters: [],
  rootLogicalOperator: LogicalOperators.AND,
}

function filterBuilderReducer(state: FilterState, action: FilterBuilderAction): FilterState {
  switch (action.type) {
    case 'ADD_FILTER':
      return addFilter(state, action.payload)

    case 'REMOVE_FILTER':
      return removeFilter(state, action.payload.filterId)

    case 'UPDATE_FILTER':
      return updateFilter(state, action.payload.filterId, action.payload.updates)

    case 'TOGGLE_ROOT_OPERATOR':
      return toggleRootLogicalOperator(state)

    case 'TOGGLE_GROUP_OPERATOR':
      return toggleGroupLogicalOperator(state, action.payload.groupId)

    case 'UNGROUP':
      return ungroupFilters(state, action.payload.groupId)

    case 'CREATE_GROUP':
      return createGroupFromSelected(state, action.payload.filterIds, action.payload.operator)

    case 'CLEAR_ALL':
      return initialState

    case 'APPLY_PRESET':
      return action.payload

    default:
      return state
  }
}

interface UseFilterBuilderOptions {
  initialState?: FilterState
  onChange?: (state: FilterState) => void
}

export function useFilterBuilderReducer(options: UseFilterBuilderOptions = {}) {
  const { initialState: initialValue, onChange } = options
  const [state, dispatch] = useReducer(filterBuilderReducer, initialValue ?? initialState)

  // Helper that dispatches and notifies parent with new state
  const dispatchAndNotify = useCallback(
    (action: FilterBuilderAction) => {
      // Compute new state using the same reducer (it's pure)
      const newState = filterBuilderReducer(state, action)
      dispatch(action)
      onChange?.(newState)
    },
    [state, onChange]
  )

  // Explicit action functions
  const actions = {
    addFilter: useCallback(
      (filter: Filter) => dispatchAndNotify({ type: 'ADD_FILTER', payload: filter }),
      [dispatchAndNotify]
    ),

    removeFilter: useCallback(
      (filterId: string) => dispatchAndNotify({ type: 'REMOVE_FILTER', payload: { filterId } }),
      [dispatchAndNotify]
    ),

    updateFilter: useCallback(
      (filterId: string, updates: Omit<Filter, 'id'>) =>
        dispatchAndNotify({ type: 'UPDATE_FILTER', payload: { filterId, updates } }),
      [dispatchAndNotify]
    ),

    toggleRootOperator: useCallback(
      () => dispatchAndNotify({ type: 'TOGGLE_ROOT_OPERATOR' }),
      [dispatchAndNotify]
    ),

    toggleGroupOperator: useCallback(
      (groupId: string) => dispatchAndNotify({ type: 'TOGGLE_GROUP_OPERATOR', payload: { groupId } }),
      [dispatchAndNotify]
    ),

    ungroup: useCallback(
      (groupId: string) => dispatchAndNotify({ type: 'UNGROUP', payload: { groupId } }),
      [dispatchAndNotify]
    ),

    createGroup: useCallback(
      (filterIds: string[], operator: LogicalOperator = LogicalOperators.AND) =>
        dispatchAndNotify({ type: 'CREATE_GROUP', payload: { filterIds, operator } }),
      [dispatchAndNotify]
    ),

    clearAll: useCallback(
      () => dispatchAndNotify({ type: 'CLEAR_ALL' }),
      [dispatchAndNotify]
    ),

    applyPreset: useCallback(
      (preset: FilterState) => dispatchAndNotify({ type: 'APPLY_PRESET', payload: preset }),
      [dispatchAndNotify]
    ),
  }

  return { state, actions }
}
