import type { CSSProperties } from 'react'
import { Text } from 'react-aria-components'
// The UNSTABLE_ toast APIs can change in a minor release. package.json pins
// react-aria-components to ~1.22.1 for this reason (notification.ts uses them too).
import {
  UNSTABLE_Toast as Toast,
  UNSTABLE_ToastContent as ToastContent,
  UNSTABLE_ToastRegion as ToastRegion,
} from 'react-aria-components/Toast'
import { CheckmarkCircle02Icon, AlertCircleIcon, InformationCircleIcon, Alert02Icon, Cancel01Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'

import { cn } from '@/lib/utils'
import { Button } from '@/components/ui/button'
import { toastQueue } from '@/components/notification'

// Icons for each toast type
const icons = {
  success: CheckmarkCircle02Icon,
  error: AlertCircleIcon,
  info: InformationCircleIcon,
  warning: Alert02Icon,
} as const

// Colors for each toast type using theme variables
const iconColors = {
  success: 'text-chart-2', // green from chart colors
  error: 'text-destructive',
  info: 'text-primary',
  warning: 'text-chart-3', // orange from chart colors
} as const

/**
 * The region that shows the toast notifications, styled with the theme of the app.
 * main.tsx renders it once, at the root of the app.
 *
 * To show a toast, call `Notification` from '@/components/notification':
 *   Notification.success('Operation completed!')
 *   Notification.error('Something went wrong')
 *   Notification.info('Here is some information')
 *   Notification.warning('Please be careful')
 */
export function Toaster() {
  // React Aria lists the newest toast first. flex-col-reverse keeps it nearest the bottom edge.
  // Below 768px the region spans the screen, and a toast is at most 500px wide. pointer-events-none lets a click beside
  // or between the toasts reach the page. Each toast takes pointer events again, so hover still pauses the timers.
  // index.css moves the region to the top while a bottom sheet is open.
  return (
    <ToastRegion
      queue={toastQueue}
      data-slot="toast-region"
      className="pointer-events-none fixed inset-x-4 bottom-4 z-[100] flex flex-col-reverse items-center gap-2 rounded-lg outline-none focus-visible:ring-3 focus-visible:ring-ring/50 md:right-6 md:bottom-6 md:left-auto"
    >
      {({ toast }) => (
        <Toast
          toast={toast}
          data-slot="toast"
          style={{ viewTransitionName: toast.key, viewTransitionClass: 'toast' } as CSSProperties}
          className={cn(
            'bg-card text-card-foreground border border-border',
            'shadow-lg rounded-lg px-4 py-3',
            'pointer-events-auto flex w-full max-w-[500px] items-start gap-3 md:min-w-[300px]',
            'outline-none focus-visible:border-ring focus-visible:ring-3 focus-visible:ring-ring/50'
          )}
        >
          <ToastContent className="flex items-start gap-3 flex-1 min-w-0">
            <HugeiconsIcon
              icon={icons[toast.content.type]}
              className={cn('size-5 shrink-0 mt-0.5', iconColors[toast.content.type])}
            />
            <div className="flex-1 min-w-0">
              <Text slot="title" className="block text-sm font-medium text-foreground">
                {toast.content.message}
              </Text>
              {toast.content.description && (
                <Text slot="description" className="mt-1 block text-sm text-muted-foreground">
                  {toast.content.description}
                </Text>
              )}
            </div>
          </ToastContent>
          <Button
            slot="close"
            aria-label="Close"
            variant="ghost"
            size="icon-xs"
            className="-m-1 shrink-0 text-muted-foreground hover:text-foreground"
          >
            <HugeiconsIcon icon={Cancel01Icon} className="size-4" />
          </Button>
        </Toast>
      )}
    </ToastRegion>
  )
}
