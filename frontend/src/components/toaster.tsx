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
// PROTOTYPE (issue #83), throwaway: localStorage 'prototype-toast-variant' picks the region position.
const prototypeVariants = {
  'top-center': 'fixed top-4 left-1/2 z-[100] flex -translate-x-1/2 flex-col items-center gap-2 outline-none',
  bottom:
    'fixed inset-x-4 bottom-[calc(1rem+env(safe-area-inset-bottom))] z-[100] flex flex-col-reverse items-stretch gap-2 outline-none sm:left-auto sm:items-end',
  'under-header': 'fixed top-[4.5rem] left-1/2 z-[100] flex -translate-x-1/2 flex-col items-center gap-2 outline-none',
} as const
type PrototypeVariant = keyof typeof prototypeVariants
const prototypeVariant = (localStorage.getItem('prototype-toast-variant') ?? 'top-center') as PrototypeVariant
document.documentElement.dataset.toastVariant = prototypeVariant

export function Toaster() {
  return (
    <ToastRegion
      queue={toastQueue}
      className={prototypeVariants[prototypeVariant]}
    >
      {({ toast }) => (
        <Toast
          toast={toast}
          style={{ viewTransitionName: toast.key, viewTransitionClass: 'toast' } as CSSProperties}
          className={cn(
            'bg-card text-card-foreground border border-border',
            'shadow-lg rounded-lg px-4 py-3',
            'flex items-start gap-3 min-w-[300px] max-w-[500px]',
            'outline-none focus-visible:ring-2 focus-visible:ring-ring'
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
