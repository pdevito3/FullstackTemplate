import { useId } from 'react'
import { Alert02Icon, TextFontIcon, Cancel01Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'
import { Button as ButtonPrimitive } from 'react-aria-components'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { cn } from '@/lib/utils'
import type { Filter } from './types'
import { getOperatorLabel } from './utils/operators'
import { formatFilterValue, getFilterName } from './utils/filter-name'
import { filterValueFault, unusedFilterMessage } from './utils/querykit-text'

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
  const fault = filterValueFault(filter)
  const messageId = useId()

  const badge = (
    <Badge
      variant="outline"
      className={cn(
        'h-auto max-w-full min-w-0 shrink gap-1.5 pr-1 py-0.5 whitespace-normal has-[[data-slot=filter-badge-edit]:hover]:bg-accent',
        fault && 'border-destructive',
      )}
    >
      <ButtonPrimitive
        data-slot="filter-badge-edit"
        onPress={onEdit}
        aria-describedby={fault ? messageId : undefined}
        className="min-w-0 cursor-pointer rounded-sm text-left outline-none focus-visible:ring-2 focus-visible:ring-ring/50"
      >
        {fault && <HugeiconsIcon icon={Alert02Icon} className="mr-1 inline size-3 align-[-0.125em] text-destructive" />}
        <span className="font-medium">{filter.propertyLabel}</span>
        <SeparatorAfterWord />
        <span className="text-xs">{getOperatorLabel(filter.operator)}</span>
        <SeparatorAfterWord />
        <span className="inline-flex max-w-full items-center gap-1.5 align-bottom">
          <span className="min-w-0 max-w-[200px] truncate text-xs">{formattedValue}</span>

          {filter.controlType === 'text' && isCaseSensitive && (
            <HugeiconsIcon icon={TextFontIcon} className="ml-1 size-3 shrink-0 text-muted-foreground" />
          )}

          {filter.controlType === 'multiselect' && filter.matchAll && (
            <span className="ml-1 shrink-0 text-[10px] font-medium text-muted-foreground bg-muted px-1 rounded">
              ALL
            </span>
          )}
        </span>
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

  if (!fault) {
    return badge
  }

  return (
    <span className="flex max-w-full min-w-0 flex-col gap-1">
      {badge}
      <span id={messageId} className="max-w-64 text-xs text-destructive">
        {unusedFilterMessage(fault)}
      </span>
    </span>
  )
}

function SeparatorAfterWord() {
  return (
    <>
      <span className="whitespace-nowrap text-xs font-semibold text-foreground/60">
        {' '}
        <span className="px-1.5">|</span>
      </span>{' '}
    </>
  )
}
