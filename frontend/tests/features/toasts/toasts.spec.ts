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

  test('keep the router devtools badge off the Close button of the toast', async ({ page }) => {
    const demo = toastDemoPage(page)
    await demo.open()
    await demo.waitForDevtoolsBadge()
    await demo.showToastsByKeyboard('Info')

    await demo.openBottomSheet()

    await expect(demo.at.closeToast('A new version is available')).toBeVisible()
    expect(await demo.overlapAreaOf(demo.at.closeToast('A new version is available'), demo.at.devtoolsBadge)).toBe(0)
  })
})

// The success toast closes after 5 s, and the error toast after 6 s.
test.describe('the toasts that a bottom sheet hides on a phone on its side', () => {
  test.use({ viewport: { width: 667, height: 375 } })

  test('show one after the other, each for its full time', async ({ page }) => {
    const demo = toastDemoPage(page)
    await demo.controlTheClock()
    await demo.open()
    await demo.showToastsByKeyboard('Success', 'Error', 'Info')

    await demo.openBottomSheet()
    const shown = await demo.shownToastsAsTimePasses(5_000, 5_000, 1_500)

    expect(shown).toEqual([
      { afterMs: 0, toasts: ['A new version is available'] },
      { afterMs: 5_000, toasts: ['Something went wrong'] },
      { afterMs: 10_000, toasts: ['Something went wrong'] },
      { afterMs: 11_500, toasts: ['Changes saved'] },
    ])
  })

  test('keep waiting after the focus leaves the toasts', async ({ page }) => {
    const demo = toastDemoPage(page)
    await demo.controlTheClock()
    await demo.open()
    await demo.showToastsByKeyboard('Success', 'Error', 'Info')

    await demo.openBottomSheet()
    await demo.moveFocusToToasts()
    await demo.pressTab(3)
    await demo.moveFocusToBottomSheet()
    const shown = await demo.shownToastsAsTimePasses(5_000, 6_500)

    expect(shown).toEqual([
      { afterMs: 0, toasts: ['A new version is available'] },
      { afterMs: 5_000, toasts: ['Something went wrong'] },
      { afterMs: 11_500, toasts: ['Changes saved'] },
    ])
  })

  test('all show again when the sheet closes, so an error that waited still shows', async ({ page }) => {
    const demo = toastDemoPage(page)
    await demo.controlTheClock()
    await demo.open()
    await demo.showToastsByKeyboard('Error', 'Success')

    await demo.openBottomSheet()
    await demo.letTimePass(2_000)
    await demo.closeBottomSheet()
    const shown = await demo.shownToastsAsTimePasses(5_000)

    expect(shown).toEqual([
      { afterMs: 0, toasts: ['Changes saved', 'Something went wrong'] },
      { afterMs: 5_000, toasts: ['Something went wrong'] },
    ])
  })
})
