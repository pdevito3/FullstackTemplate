import { expect, test } from '@tests/support/fixtures'
import type { Overlay } from './overlays.locators'
import { overlayDemoPage } from './overlays.tasks'

// The demo drawer has no Close button at the top. Its Done button is at the bottom.
const overlays: { opens: Overlay; name: string; hasCloseButton: boolean }[] = [
  { opens: 'Sheet', name: 'sheet', hasCloseButton: true },
  { opens: 'Drawer', name: 'drawer', hasCloseButton: false },
  { opens: 'Dialog', name: 'responsive dialog', hasCloseButton: true },
]

for (const viewport of [
  { width: 320, height: 568 },
  { width: 1280, height: 800 },
]) {
  test.describe(`the router devtools badge at ${viewport.width}x${viewport.height}`, () => {
    test.use({ viewport })

    for (const { opens, name, hasCloseButton } of overlays) {
      test(`keeps off the title and any Close button of the ${name}`, async ({ page }) => {
        const demo = overlayDemoPage(page)
        await demo.open()

        await demo.openOverlay(opens)

        await expect(demo.at.title).toBeVisible()
        await expect.poll(() => demo.overlapAreaOf(demo.at.title, demo.at.devtoolsBadge)).toBe(0)
        if (hasCloseButton) {
          await expect(demo.at.closeButton).toBeVisible()
          await expect.poll(() => demo.overlapAreaOf(demo.at.closeButton, demo.at.devtoolsBadge)).toBe(0)
        }
      })
    }

    test('shows again when the sheet closes', async ({ page }) => {
      const demo = overlayDemoPage(page)
      await demo.open()
      await demo.openOverlay('Sheet')

      await demo.closeOverlay()

      await expect(demo.at.devtoolsBadge).toBeVisible()
    })
  })
}
