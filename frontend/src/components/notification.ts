import { flushSync } from 'react-dom'
import { UNSTABLE_ToastQueue as ToastQueue } from 'react-aria-components/Toast'

export type ToastType = 'success' | 'error' | 'info' | 'warning'

interface ToastMessage {
  message: string
  type: ToastType
  description?: string
}

interface NotificationOptions {
  description?: string
  /** Milliseconds before the toast closes. React Aria recommends 5000 or more. */
  duration?: number
}

/**
 * The queue of toasts that `Toaster` shows. Each update runs in a view
 * transition, so the toasts animate in and out. With reduced motion, the
 * toasts show and close without an animation.
 */
export const toastQueue = new ToastQueue<ToastMessage>({
  maxVisibleToasts: 5,
  wrapUpdate(fn) {
    if (
      'startViewTransition' in document &&
      !window.matchMedia('(prefers-reduced-motion: reduce)').matches
    ) {
      const transition = document.startViewTransition(() => {
        flushSync(fn)
      })
      // A second update before this transition ends skips it, and `ready` rejects. The update still runs, so only the
      // animation is lost. `updateCallbackDone` and `finished` reject only when the update throws, so they stay unhandled.
      transition.ready.catch(() => {})
    } else {
      fn()
    }
  },
})

function createToast(message: string, type: ToastType, options?: NotificationOptions): string {
  const { description, duration = type === 'error' ? 6000 : 5000 } = options ?? {}
  // While a toast is visible, the same toast again shows nothing. A query that retries does not show its error once for each attempt.
  const shown = toastQueue.visibleToasts.find(
    ({ content }) => content.message === message && content.type === type && content.description === description,
  )
  if (shown) return shown.key
  return toastQueue.add({ message, type, description }, { timeout: duration })
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
}
