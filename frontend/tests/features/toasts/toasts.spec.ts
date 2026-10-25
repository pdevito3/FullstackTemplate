import { expect, test } from '@tests/support/fixtures'
import { toastDemoPage } from './toasts.tasks'

test.describe('the toast region', () => {
  test('shows a focus ring when F6 moves the focus to it', async ({ page }) => {
    const demo = toastDemoPage(page)
    await demo.open()
    await demo.showToastsByKeyboard('Success')

    await demo.moveFocusToToasts()

    expect(await demo.hasFocusRing(demo.at.region)).toBe(true)
  })
})
