import {
  ResponsiveDialog,
  ResponsiveDialogContent,
  ResponsiveDialogHeader,
  ResponsiveDialogTitle,
} from '@/components/ui/responsive-dialog'
import type { Filter, FilterConfig } from '../types'
import { BooleanFilter } from './boolean-filter'
import { DateFilter } from './date-filter'
import { MultiSelectFilter } from './multiselect-filter'
import { NumberFilter } from './number-filter'
import { TextFilterModal } from './text-filter-modal'

interface FilterEditModalProps {
  isOpen: boolean
  onClose: () => void
  onSubmit: (filter: Omit<Filter, 'id'>) => void
  filter: Filter
  filterOptions: FilterConfig[]
}

export function FilterEditModal({
  isOpen,
  onClose,
  onSubmit,
  filter,
  filterOptions,
}: FilterEditModalProps) {
  const handleSubmit = (updatedFilter: Omit<Filter, 'id'>) => {
    onSubmit(updatedFilter)
    onClose()
  }

  // For text filters, use the existing TextFilterModal which has its own Dialog wrapper
  if (filter.controlType === 'text') {
    return (
      <TextFilterModal
        isOpen={isOpen}
        onClose={onClose}
        onSubmit={handleSubmit}
        propertyKey={filter.propertyKey}
        propertyLabel={filter.propertyLabel}
        initialFilter={filter}
      />
    )
  }

  // For other types, wrap them in a Dialog
  // Find the filter config to get options for multiselect
  const filterConfig = filterOptions.find(
    (opt) => opt.propertyKey === filter.propertyKey
  )

  return (
    <ResponsiveDialog open={isOpen} onOpenChange={onClose}>
      <ResponsiveDialogContent className="md:w-auto md:max-w-fit">
        <ResponsiveDialogHeader>
          <ResponsiveDialogTitle>Edit {filter.propertyLabel} Filter</ResponsiveDialogTitle>
        </ResponsiveDialogHeader>

        {filter.controlType === 'boolean' && (
          <BooleanFilter
            propertyKey={filter.propertyKey}
            propertyLabel={filter.propertyLabel}
            onSubmit={handleSubmit}
            initialFilter={filter}
          />
        )}

        {filter.controlType === 'date' && (
          <DateFilter
            propertyKey={filter.propertyKey}
            propertyLabel={filter.propertyLabel}
            onSubmit={handleSubmit}
            initialFilter={filter}
            dateType={filterConfig?.dateType}
          />
        )}

        {filter.controlType === 'number' && (
          <NumberFilter
            propertyKey={filter.propertyKey}
            propertyLabel={filter.propertyLabel}
            onSubmit={handleSubmit}
            initialFilter={filter}
            allowedOperators={filterConfig?.operators}
          />
        )}

        {filter.controlType === 'multiselect' && filterConfig?.options && (
          <MultiSelectFilter
            propertyKey={filter.propertyKey}
            propertyLabel={filter.propertyLabel}
            options={filterConfig.options}
            onSubmit={handleSubmit}
            initialFilter={filter}
            allowedOperators={filterConfig.operators}
          />
        )}
      </ResponsiveDialogContent>
    </ResponsiveDialog>
  )
}
