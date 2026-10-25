import { Suspense } from 'react'
import {
  ResponsiveDialog,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
} from '@/components/ui/responsive-dialog'
import { Skeleton } from '@/components/ui/skeleton'
import type { FilterControlProps } from '../types'
import { FILTER_CONTROLS } from './filter-controls'

const DEFAULT_DIALOG_CLASS_NAME = 'md:w-max md:max-w-[calc(100vw-2rem)]'

/**
 * Shows the filter control that matches `config.controlType`. A lazy control
 * (date) shows a placeholder while its chunk loads.
 */
export function FilterControl(props: FilterControlProps) {
  const { component: Control } = FILTER_CONTROLS[props.config.controlType]

  return (
    <Suspense fallback={<FilterControlFallback />}>
      <Control {...props} />
    </Suspense>
  )
}

/** About the size of the date control, so that the popover does not jump when it loads. */
function FilterControlFallback() {
  return (
    <div role="status" aria-label="Loading filter" className="p-3">
      <Skeleton className="h-106 w-88" />
    </div>
  )
}

interface FilterControlDialogProps extends Omit<FilterControlProps, 'autoFocus' | 'onCancel'> {
  onCancel: () => void
}

/** A dialog that adds a filter, or edits `initialFilter`, with its filter control. */
export function FilterControlDialog({
  config,
  initialFilter,
  onSubmit,
  onCancel,
}: FilterControlDialogProps) {
  return (
    <ResponsiveDialog
      isOpen
      onOpenChange={(open) => {
        if (!open) {
          onCancel()
        }
      }}
      className={FILTER_CONTROLS[config.controlType].dialogClassName ?? DEFAULT_DIALOG_CLASS_NAME}
    >
      <div className="flex flex-col gap-4">
        <ResponsiveDialogHeader>
          <ResponsiveDialogTitle>
            {initialFilter ? 'Edit' : 'Add'} {config.propertyLabel} Filter
          </ResponsiveDialogTitle>
        </ResponsiveDialogHeader>
        <FilterControl
          config={config}
          initialFilter={initialFilter}
          onSubmit={onSubmit}
          onCancel={onCancel}
          autoFocus
        />
      </div>
    </ResponsiveDialog>
  )
}
