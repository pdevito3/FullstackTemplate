import type { ReactNode } from 'react'
import { ResponsiveDialogFooter } from '@/components/ui/responsive-dialog'
import type { FilterControlSurface } from '../types'

interface FilterControlFooterProps {
  surface: FilterControlSurface
  children: ReactNode
}

/**
 * The submit row of a filter control. Put it last in the control. In a dialog it sticks to the bottom of the content
 * that scrolls, so a tall control does not push its submit button off the screen. In the submenu the row stays in the
 * flow, and the popover scrolls as a whole.
 */
export function FilterControlFooter({ surface, children }: FilterControlFooterProps) {
  if (surface === 'dialog') {
    return <ResponsiveDialogFooter>{children}</ResponsiveDialogFooter>
  }

  return <div className="flex gap-2 pt-3">{children}</div>
}
