import { TextFontIcon, Cancel01Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { Button as ButtonPrimitive } from 'react-aria-components'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import type { Filter } from './types'
import { getOperatorLabel } from './utils/operators'
import { formatFilterValue, getFilterName } from './utils/filter-name'

interface FilterBadgeProps {
  filter: Filter
  onRemove: () => void
  onEdit?: () => void
}

export function FilterBadge({ filter, onRemove, onEdit }: FilterBadgeProps) {
  const formattedValue = formatFilterValue(filter)
  // A filter is case-sensitive if:
  // 1. The caseSensitive flag is explicitly true, OR
  // 2. The operator doesn't end with * (which indicates case-insensitive)
  const isCaseSensitive =
    filter.caseSensitive === true ||
    (filter.controlType === 'text' && !filter.operator.endsWith('*'))

  return (
    <Badge
      variant="outline"
      className="gap-1.5 pr-1 py-0.5 has-[[data-slot=filter-badge-edit]:hover]:bg-accent"
    >
      <ButtonPrimitive
        data-slot="filter-badge-edit"
        onPress={onEdit}
        className="flex cursor-pointer items-center gap-1.5 rounded-sm outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
      >
        <span className="font-medium">{filter.propertyLabel}</span>
        <span className="text-foreground/60 font-semibold px-1 text-xs">
          |
        </span>
        <span className="text-xs">{getOperatorLabel(filter.operator)}</span>
        <span className="text-foreground/60 font-semibold px-1 text-xs">
          |
        </span>
        <span className="max-w-[200px] truncate text-xs">{formattedValue}</span>

        {filter.controlType === 'text' && isCaseSensitive && (
          <HugeiconsIcon icon={TextFontIcon} className="size-3 text-muted-foreground ml-1" />
        )}

        {filter.controlType === 'multiselect' && filter.matchAll && (
          <span className="text-[10px] font-medium text-muted-foreground ml-1 bg-muted px-1 rounded">
            ALL
          </span>
        )}
      </ButtonPrimitive>

      <Button
        variant="ghost"
        size="icon-xs"
        className="size-4 ml-1 hover:bg-destructive/20 hover:text-destructive rounded-sm"
        aria-label={`Remove filter: ${getFilterName(filter)}`}
        onPress={onRemove}
      >
        <HugeiconsIcon icon={Cancel01Icon} className="size-3" />
      </Button>
    </Badge>
  )
}
