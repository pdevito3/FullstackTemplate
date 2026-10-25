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

// React Aria lists the newest toast first.
test.describe('the toasts on a phone on its side while a bottom sheet is open', () => {
  test.use({ viewport: { width: 667, height: 375 } })

  test('show only the newest toast, and keep the others for a screen reader', async ({ page }) => {
    const demo = toastDemoPage(page)
    await demo.open()
    await demo.showToastsByKeyboard('Success', 'Error', 'Info')

    await demo.openBottomSheet()

    expect(await demo.shownToasts()).toEqual(['A new version is available'])
    expect(await demo.at.toasts.count()).toBe(3)
  })

  test('show an older toast while it has the keyboard focus', async ({ page }) => {
    const demo = toastDemoPage(page)
    await demo.open()
    await demo.showToastsByKeyboard('Success', 'Error', 'Info')
    await demo.openBottomSheet()

    await demo.moveFocusToToasts()
    await demo.pressTab(3)

    await expect(demo.at.toast('Something went wrong')).toBeFocused()
    expect(await demo.shownToasts()).toEqual(['A new version is available', 'Something went wrong'])
  })
})
