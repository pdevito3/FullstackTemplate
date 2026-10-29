import { UnfoldMoreIcon, Cancel01Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Tooltip, TooltipTrigger } from '@/components/ui/tooltip'
import { Card } from '@/components/ui/card'
import { Checkbox } from '@/components/ui/checkbox'
import { cn } from '@/lib/utils'
import { FilterBadge } from './filter-badge'
import type {
  Filter,
  FilterConfig,
  FilterGroup as FilterGroupType,
} from './types'
import { isFilter } from './types'
import { getFilterName } from './utils/filter-name'

interface FilterGroupProps {
  group: FilterGroupType
  depth: number
  selectedIds: Set<string>
  onToggleSelection: (id: string) => void
  onToggleOperator: (groupId: string) => void
  onRemove: (groupId: string) => void
  onUngroup: (groupId: string) => void
  onRemoveFilter: (filterId: string) => void
  onEditFilter: (filter: Filter) => void
  filterOptions: FilterConfig[]
  showCheckbox?: boolean
}

export function FilterGroup({
  group,
  depth,
  selectedIds,
  onToggleSelection,
  onToggleOperator,
  onRemove,
  onUngroup,
  onRemoveFilter,
  onEditFilter,
  filterOptions,
  showCheckbox = true,
}: FilterGroupProps) {
  const isSelected = selectedIds.has(group.id)

  // A color for each depth (blue, purple, pink) from the theme chart palette,
  // so that the dark theme changes it too.
  const depthColors = [
    'border-l-chart-1 bg-chart-1/5',
    'border-l-chart-4 bg-chart-4/5',
    'border-l-chart-5 bg-chart-5/5',
  ]
  const depthColor = depthColors[depth % depthColors.length]

  return (
    <Card
      role="group"
      aria-label={`Group (Depth ${depth})`}
      className={cn(
        'border-l-4 transition-all p-0',
        depthColor,
        isSelected && 'ring-2 ring-primary'
      )}
    >
      <div className="p-2">
        {/* Group Header */}
        {/* In a narrow group, the actions wrap below the details, and each AND wraps below its filter. */}
        <div data-slot="filter-group-header" className="flex flex-wrap items-start gap-1.5 mb-1.5">
          <div className="flex flex-1 flex-wrap items-center gap-1.5">
            {/* Selection Checkbox */}
            {showCheckbox && (
              <Checkbox
                aria-label="Select group"
                isSelected={isSelected}
                onChange={() => onToggleSelection(group.id)}
                className="shrink-0"
              />
            )}

            {/* Group Label */}
            <Badge variant="outline" className="text-xs font-medium shrink-0">
              Group (Depth {depth})
            </Badge>

            {/* Logical Operator Toggle */}
            <Button
              variant="ghost"
              size="xs"
              onPress={() => onToggleOperator(group.id)}
              className="h-6 px-2 text-xs font-medium shrink-0"
            >
              {group.logicalOperator}
            </Button>

            {/* Filter Count */}
            <span className="text-xs text-muted-foreground shrink-0">
              {group.filters.length}{' '}
              {group.filters.length === 1 ? 'filter' : 'filters'}
            </span>
          </div>

          <div className="ml-auto flex flex-wrap items-center gap-1.5">
            {/* Ungroup Button */}
            <TooltipTrigger>
              <Button
                variant="ghost"
                size="xs"
                onPress={() => onUngroup(group.id)}
                className="h-6 px-2 text-xs shrink-0"
              >
                <HugeiconsIcon icon={UnfoldMoreIcon} className="size-3" />
                Ungroup
              </Button>
              <Tooltip>Move the filters to the parent group</Tooltip>
            </TooltipTrigger>

            {/* Remove Group Button */}
            <TooltipTrigger>
              <Button
                variant="ghost"
                size="icon-xs"
                onPress={() => onRemove(group.id)}
                aria-label="Delete group"
                className="h-6 w-6 p-0 hover:bg-destructive/20 hover:text-destructive shrink-0"
              >
                <HugeiconsIcon icon={Cancel01Icon} className="size-3" />
              </Button>
              <Tooltip>Delete the group and all its filters</Tooltip>
            </TooltipTrigger>
          </div>
        </div>

        {/* Group Contents - Horizontal Layout */}
        <div className="flex flex-wrap items-start gap-2 pl-3 sm:pl-6">
          {group.filters.map((item, index) => (
            <div key={item.id} className="flex max-w-full min-w-0 flex-wrap items-center gap-2">
              {isFilter(item) ? (
                <div className="flex min-w-0 items-center gap-2">
                  {showCheckbox && (
                    <Checkbox
                      aria-label={`Select filter: ${getFilterName(item)}`}
                      isSelected={selectedIds.has(item.id)}
                      onChange={() => onToggleSelection(item.id)}
                      className="shrink-0"
                    />
                  )}
                  <FilterBadge
                    filter={item}
                    onRemove={() => onRemoveFilter(item.id)}
                    onEdit={() => onEditFilter(item)}
                  />
                </div>
              ) : (
                <div className="w-full">
                  <FilterGroup
                    group={item}
                    depth={depth + 1}
                    selectedIds={selectedIds}
                    onToggleSelection={onToggleSelection}
                    onToggleOperator={onToggleOperator}
                    onRemove={onRemove}
                    onUngroup={onUngroup}
                    onRemoveFilter={onRemoveFilter}
                    onEditFilter={onEditFilter}
                    filterOptions={filterOptions}
                    showCheckbox={showCheckbox}
                  />
                </div>
              )}

              {/* Show logical operator between items */}
              {index < group.filters.length - 1 && (
                <span className="shrink-0 text-xs text-muted-foreground font-medium px-1">
                  {group.logicalOperator}
                </span>
              )}
            </div>
          ))}

          {group.filters.length === 0 && (
            <div className="text-xs text-muted-foreground italic">
              Empty group (will be removed on save)
            </div>
          )}
        </div>
      </div>
    </Card>
  )
}
