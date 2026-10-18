import { Toaster as HotToaster } from 'react-hot-toast'
import { cn } from '@/lib/utils'
import { CheckmarkCircle02Icon, AlertCircleIcon, InformationCircleIcon, Alert02Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'

/**
 * The container that shows the toast notifications, styled with the theme of the app.
 * main.tsx renders it once, at the root of the app.
 *
 * To show a toast, call `Notification` from '@/components/notification':
 *   Notification.success('Operation completed!')
 *   Notification.error('Something went wrong')
 *   Notification.info('Here is some information')
 *   Notification.warning('Please be careful')
 */
export function Toaster() {
  return (
    <HotToaster
      position="top-center"
      gutter={8}
      containerClassName=""
      toastOptions={{
        duration: 4000,
        className: cn(
          'bg-card text-card-foreground border border-border',
          'shadow-lg rounded-lg px-4 py-3',
          'flex items-start gap-3 min-w-[300px] max-w-[500px]'
        ),
      }}
    />
  )
}

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

export type ToastType = keyof typeof icons

interface ToastContentProps {
  message: string
  type: ToastType
  description?: string
}

export function ToastContent({ message, type, description }: ToastContentProps) {
  const icon = icons[type]
  const iconColor = iconColors[type]

  return (
    <div className="flex items-start gap-3 flex-1">
      <HugeiconsIcon icon={icon} className={cn('size-5 shrink-0 mt-0.5', iconColor)} />
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium text-foreground">{message}</p>
        {description && (
          <p className="text-sm text-muted-foreground mt-1">{description}</p>
        )}
      </div>
    </div>
  )
}
