import { useState } from 'react'
import { FolderAddIcon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { cn } from '@/lib/utils'
import { FilterEditModal } from './controls/filter-edit-modal'
import { FilterBadge } from './filter-badge'
import { FilterGroup } from './filter-group'
import { FilterPropertyMenu } from './filter-property-menu'
import type { Filter, FilterBuilderProps } from './types'
import { isFilter } from './types'
import { canCreateGroup } from './utils/depth'
import { toQueryKitString } from './utils/querykit-converter'
import { LogicalOperators } from './utils/operators'
import { useFilterBuilderReducer } from './use-filter-builder-reducer'

export function FilterBuilder({
  filterOptions,
  presets = [],
  onChange,
  initialState,
  className,
}: FilterBuilderProps) {
  const { state, actions } = useFilterBuilderReducer({ initialState, onChange })

  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set())
  const [editingFilter, setEditingFilter] = useState<Filter | null>(null)
  const [editModalOpen, setEditModalOpen] = useState(false)
  const [isGroupingMode, setIsGroupingMode] = useState(false)

  const handleAddFilter = (filter: Omit<Filter, 'id'>) => {
    const newFilter: Filter = {
      ...filter,
      id: crypto.randomUUID(),
    }
    actions.addFilter(newFilter)
  }

  const handleRemoveFilter = (filterId: string) => {
    actions.removeFilter(filterId)
    setSelectedIds((prev) => {
      const next = new Set(prev)
      next.delete(filterId)
      return next
    })
  }

  const handleToggleLogicalOperator = () => {
    actions.toggleRootOperator()
  }

  const handleToggleGroupOperator = (groupId: string) => {
    actions.toggleGroupOperator(groupId)
  }

  const handleUngroup = (groupId: string) => {
    actions.ungroup(groupId)
    setSelectedIds((prev) => {
      const next = new Set(prev)
      next.delete(groupId)
      return next
    })
  }

  const handleEditFilter = (filter: Filter) => {
    setEditingFilter(filter)
    setEditModalOpen(true)
  }

  const handleUpdateFilter = (updatedFilter: Omit<Filter, 'id'>) => {
    if (!editingFilter) return

    actions.updateFilter(editingFilter.id, updatedFilter)
    setEditModalOpen(false)
    setEditingFilter(null)
  }

  const handleToggleSelection = (id: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev)
      if (next.has(id)) {
        next.delete(id)
      } else {
        next.add(id)
      }
      return next
    })
  }

  const handleEnterGroupingMode = () => {
    setIsGroupingMode(true)
  }

  const handleCancelGrouping = () => {
    setIsGroupingMode(false)
    setSelectedIds(new Set())
  }

  const handleClearAll = () => {
    actions.clearAll()
    setSelectedIds(new Set())
  }

  const handleCreateGroup = () => {
    const ids = Array.from(selectedIds)
    const { canCreate, reason } = canCreateGroup(state, ids)

    if (!canCreate) {
      alert(reason || 'Cannot create group')
      return
    }

    actions.createGroup(ids, LogicalOperators.AND)
    setSelectedIds(new Set())
    setIsGroupingMode(false)
  }

  const hasFilters = state.filters.length > 0
  const hasMultipleFilters = state.filters.length > 1
  const selectedCount = selectedIds.size
  const canGroup = selectedCount >= 2

  return (
    <div className={cn('space-y-2', className)}>
      <div className="flex items-center gap-2 flex-wrap">
        <FilterPropertyMenu
          options={filterOptions}
          onAddFilter={handleAddFilter}
        />

        {/* Grouping Controls */}
        {hasFilters && !isGroupingMode && (
          <>
            <Button
              variant="outline"
              size="sm"
              onClick={handleEnterGroupingMode}
            >
              <HugeiconsIcon icon={FolderAddIcon} className="size-4 mr-2" />
              Group Filters
            </Button>

            <Button
              variant="outline"
              size="sm"
              onClick={handleClearAll}
            >
              Clear
            </Button>
          </>
        )}

        {isGroupingMode && (
          <>
            <Button
              variant="default"
              size="sm"
              onClick={handleCreateGroup}
              disabled={!canGroup}
            >
              <HugeiconsIcon icon={FolderAddIcon} className="size-4 mr-2" />
              {selectedCount > 0
                ? `Group Selected (${selectedCount})`
                : 'Group Filters'}
            </Button>

            <Button
              variant="outline"
              size="sm"
              onClick={handleCancelGrouping}
            >
              Cancel
            </Button>
          </>
        )}

        {/* Logical operator toggle button */}
        {hasMultipleFilters && (
          <Button
            variant="ghost"
            size="sm"
            onClick={handleToggleLogicalOperator}
            className="h-7 px-2 text-xs"
          >
            Switch to {state.rootLogicalOperator === LogicalOperators.AND ? LogicalOperators.OR : LogicalOperators.AND}
          </Button>
        )}
      </div>

      <div className="flex flex-wrap items-start gap-3">
        {state.filters.map((item, index) => (
          <div key={item.id} className="flex items-center gap-1.5">
            {isFilter(item) ? (
              <div className="flex items-center gap-1.5">
                {isGroupingMode && (
                  <Checkbox
                    isSelected={selectedIds.has(item.id)}
                    onChange={() => handleToggleSelection(item.id)}
                    className="shrink-0"
                  />
                )}
                <FilterBadge
                  filter={item}
                  onRemove={() => handleRemoveFilter(item.id)}
                  onEdit={() => handleEditFilter(item)}
                />
              </div>
            ) : (
              <div className="w-full">
                <FilterGroup
                  group={item}
                  depth={1}
                  selectedIds={selectedIds}
                  onToggleSelection={handleToggleSelection}
                  onToggleOperator={handleToggleGroupOperator}
                  onRemove={handleRemoveFilter}
                  onUngroup={handleUngroup}
                  onRemoveFilter={handleRemoveFilter}
                  onEditFilter={handleEditFilter}
                  filterOptions={filterOptions}
                  showCheckbox={isGroupingMode}
                />
              </div>
            )}

            {/* Show logical operator between items */}
            {index < state.filters.length - 1 && (
              <span className="text-sm text-muted-foreground font-medium px-1">
                {state.rootLogicalOperator}
              </span>
            )}
          </div>
        ))}
      </div>

      {/* Quick filter presets */}
      {presets.length > 0 && (
        <div className="flex gap-2 items-center flex-wrap">
          <span className="text-sm text-muted-foreground">Quick filters:</span>
          {presets.map((preset) => (
            <Button
              key={preset.label}
              variant="outline"
              size="sm"
              onClick={() => {
                actions.applyPreset(preset.filter)
                setSelectedIds(new Set())
              }}
            >
              {preset.label}
            </Button>
          ))}
        </div>
      )}

      {/* Empty state */}
      {!hasFilters && (
        <div className="text-sm text-muted-foreground">
          No filters applied. Click "Add Filter" to get started.
        </div>
      )}

      {/* Edit filter modal (supports all filter types) */}
      {editingFilter && (
        <FilterEditModal
          isOpen={editModalOpen}
          onClose={() => {
            setEditModalOpen(false)
            setEditingFilter(null)
          }}
          onSubmit={handleUpdateFilter}
          filter={editingFilter}
          filterOptions={filterOptions}
        />
      )}
    </div>
  )
}

// Export utility function to convert state to QueryKit string
export { toQueryKitString }
