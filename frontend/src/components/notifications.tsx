import toast, { Toaster, type ToastOptions } from 'react-hot-toast'
import { cn } from '@/lib/utils'
import { Cancel01Icon, CheckmarkCircle02Icon, AlertCircleIcon, InformationCircleIcon, Alert02Icon } from '@hugeicons/core-free-icons'
import { HugeiconsIcon } from '@hugeicons/react'

/**
 * Toast notification component styled with the app's theme.
 * Place this component once at the root of your app (e.g., in main.tsx).
 *
 * Usage:
 *   Notification.success('Operation completed!')
 *   Notification.error('Something went wrong')
 *   Notification.info('Here is some information')
 *   Notification.warning('Please be careful')
 */
export function Notifications() {
  return (
    <Toaster
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

type ToastType = keyof typeof icons

interface ToastContentProps {
  message: string
  type: ToastType
  description?: string
}

function ToastContent({ message, type, description }: ToastContentProps) {
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

type NotificationOptions = ToastOptions & {
  description?: string
}

function createToast(
  message: string,
  type: ToastType,
  options?: NotificationOptions
) {
  const { description, ...toastOptions } = options || {}

  return toast.custom(
    (t) => (
      <div
        className={cn(
          'bg-card text-card-foreground border border-border',
          'shadow-lg rounded-lg px-4 py-3',
          'flex items-start gap-3 min-w-[300px] max-w-[500px]',
          'transform transition-all duration-200',
          t.visible ? 'animate-in fade-in-0 slide-in-from-top-2' : 'animate-out fade-out-0 slide-out-to-top-2'
        )}
      >
        <ToastContent message={message} type={type} description={description} />
        <button
          onClick={() => toast.dismiss(t.id)}
          className={cn(
            'shrink-0 rounded-md p-1 -m-1',
            'text-muted-foreground hover:text-foreground',
            'hover:bg-muted transition-colors',
            'focus:outline-none focus:ring-2 focus:ring-ring focus:ring-offset-2 focus:ring-offset-background'
          )}
        >
          <HugeiconsIcon icon={Cancel01Icon} className="size-4" />
        </button>
      </div>
    ),
    {
      duration: type === 'error' ? 6000 : 4000,
      ...toastOptions,
    }
  )
}

/**
 * Notification utility for showing toast messages.
 *
 * @example
 * // Success notification
 * Notification.success('Changes saved successfully')
 *
 * // Error notification
 * Notification.error('Failed to save changes')
 *
 * // With description
 * Notification.error('Operation failed', { description: 'Please try again later' })
 *
 * // With custom duration
 * Notification.info('Processing...', { duration: 10000 })
 */
export const Notification = {
  success: (message: string, options?: NotificationOptions) =>
    createToast(message, 'success', options),

  error: (message: string, options?: NotificationOptions) =>
    createToast(message, 'error', options),

  info: (message: string, options?: NotificationOptions) =>
    createToast(message, 'info', options),

  warning: (message: string, options?: NotificationOptions) =>
    createToast(message, 'warning', options),

  /**
   * Dismiss a specific toast by ID or all toasts if no ID provided
   */
  dismiss: (toastId?: string) => toast.dismiss(toastId),

  /**
   * Promise-based toast that shows loading, success, and error states
   */
  promise: <T,>(
    promise: Promise<T>,
    messages: {
      loading: string
      success: string | ((data: T) => string)
      error: string | ((err: unknown) => string)
    },
    options?: NotificationOptions
  ) =>
    toast.promise(
      promise,
      {
        loading: messages.loading,
        success: messages.success,
        error: messages.error,
      },
      {
        duration: 4000,
        ...options,
      }
    ),
}
