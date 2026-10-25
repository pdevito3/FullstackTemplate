import { lazy, type ComponentType } from 'react'
import type { ControlType, FilterControlProps } from '../types'
import { BooleanFilter } from './boolean-filter'
import { MultiSelectFilter } from './multiselect-filter'
import { NumberFilter } from './number-filter'
import { TextFilter } from './text-filter'

// The date control brings the calendar and @internationalized/date. Load them
// only when a date control shows. FilterControl gives the Suspense fallback.
const DateFilter = lazy(async () => {
  const { DateFilter } = await import('./date-filter')
  return { default: DateFilter }
})

export type FilterControlSurface = 'submenu' | 'dialog'

interface FilterControlDefinition {
  component: ComponentType<FilterControlProps>
  /**
   * Where the Add Filter menu shows the control on a desktop: in a submenu next
   * to the menu, or in a dialog after the menu closes. The edit dialog shows all types.
   */
  surface: FilterControlSurface
  /**
   * The class of FilterControlDialog. Without it, the dialog fits the width of
   * the control (`md:w-auto md:max-w-fit`).
   */
  dialogClassName?: string
}

/**
 * The control of each control type. To add a control type, add it here.
 * `Record<ControlType, ...>` makes TypeScript require an entry for each type.
 */
export const FILTER_CONTROLS: Record<ControlType, FilterControlDefinition> = {
  // The text control has no fixed width, so its dialog keeps the default width.
  text: { component: TextFilter, surface: 'dialog', dialogClassName: 'md:w-full md:max-w-md' },
  multiselect: { component: MultiSelectFilter, surface: 'submenu' },
  date: { component: DateFilter, surface: 'submenu' },
  number: { component: NumberFilter, surface: 'submenu' },
  boolean: { component: BooleanFilter, surface: 'submenu' },
}
