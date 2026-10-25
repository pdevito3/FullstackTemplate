import { expect, onTestFinished, test, vi } from 'vitest'
import { page } from 'vitest/browser'
import { render } from 'vitest-browser-react'

import { Notification, toastQueue } from '@/components/notification'
import { Toaster } from '@/components/toaster'

test('two toasts in the same tick both show, and the skipped view transition raises no unhandled rejection', async () => {
  const unhandled: unknown[] = []
  const handleRejection = (event: PromiseRejectionEvent) => {
    unhandled.push(event.reason)
    event.preventDefault()
  }
  window.addEventListener('unhandledrejection', handleRejection)
  onTestFinished(() => window.removeEventListener('unhandledrejection', handleRejection))
  const startViewTransition = vi.spyOn(document, 'startViewTransition')
  onTestFinished(() => {
    startViewTransition.mockRestore()
    toastQueue.clear()
  })
  await render(<Toaster />)

  Notification.success('First change saved')
  Notification.success('Second change saved')

  await expect.element(page.getByText('First change saved')).toBeVisible()
  await expect.element(page.getByText('Second change saved')).toBeVisible()
  // The second update skips the first transition. The browser reports an unhandled rejection a task after the skip, long before the second transition finishes.
  expect(startViewTransition).toHaveBeenCalledTimes(2)
  await Promise.all(startViewTransition.mock.results.map(({ value }) => value.finished))
  expect(unhandled).toEqual([])
})
