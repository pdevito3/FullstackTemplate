import { flushSync } from 'react-dom'
import { UNSTABLE_ToastQueue as ToastQueue, type QueuedToast } from 'react-aria-components/Toast'

export type ToastType = 'success' | 'error' | 'info' | 'warning'

export interface ToastMessage {
  message: string
  type: ToastType
  description?: string
}

interface NotificationOptions {
  description?: string
  /** Milliseconds before the toast closes. React Aria recommends 5000 or more. */
  duration?: number
}

// While a bottom sheet is open on a phone, index.css hides each toast but the newest. A hidden toast must not time out.
class ShownToastQueue<T> extends ToastQueue<T> {
  // A toast has no entry until it renders. React Aria starts its timer when it mounts.
  readonly #visibility = new WeakMap<QueuedToast<T>, 'shown' | 'hidden'>()
  #isPaused = false

  markHidden(toast: QueuedToast<T>): void {
    this.#visibility.set(toast, 'hidden')
    toast.timer?.pause()
  }

  markShown(toast: QueuedToast<T>): void {
    const wasHidden = this.#visibility.get(toast) === 'hidden'
    this.#visibility.set(toast, 'shown')
    if (!wasHidden || !toast.timer || !toast.timeout) return
    // React Aria starts the timer again when a toast mounts again, and reset does not clear a running timer.
    toast.timer.pause()
    toast.timer.reset(toast.timeout)
    if (this.#isPaused) toast.timer.pause()
  }

  override pauseAll(): void {
    this.#isPaused = true
    super.pauseAll()
  }

  override resumeAll(): void {
    this.#isPaused = false
    for (const toast of this.visibleToasts) {
      if (this.#visibility.get(toast) !== 'shown' || !toast.timer) continue
      // A toast that mounts while the toasts pause starts its timer anyway, and resume does not clear a running timer.
      toast.timer.pause()
      toast.timer.resume()
    }
  }
}

/**
 * The queue of toasts that `Toaster` shows. Each update runs in a view
 * transition, so the toasts animate in and out. With reduced motion, the
 * toasts show and close without an animation.
 */
export const toastQueue = new ShownToastQueue<ToastMessage>({
  maxVisibleToasts: 5,
  wrapUpdate(fn) {
    if (
      'startViewTransition' in document &&
      !window.matchMedia('(prefers-reduced-motion: reduce)').matches
    ) {
      const transition = document.startViewTransition(() => {
        flushSync(fn)
      })
      // The browser skips the animation and rejects `ready` when a later transition starts or the page is hidden.
      // The update still runs.
      // `finished` and `updateCallbackDone` reject only when the update throws, so leave them unhandled.
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
