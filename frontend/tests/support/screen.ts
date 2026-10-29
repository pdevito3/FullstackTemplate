import type { Locator, Page } from '@playwright/test'

// A visually hidden element (Tailwind sr-only) is 1 px by 1 px, and Playwright counts it as visible.
const visuallyHiddenSizePx = 1

export async function takesUpSpace(element: Locator): Promise<boolean> {
  const box = await element.boundingBox()
  return box !== null && box.width > visuallyHiddenSizePx && box.height > visuallyHiddenSizePx
}

// Chromium only (CDP), as a user sets the default font size of the browser. The fixed font keeps the 13:16 ratio
// to the standard font that Chromium has by default.
export async function useDefaultFontSize(page: Page, px: number) {
  const cdp = await page.context().newCDPSession(page)
  await cdp.send('Page.setFontSizes', { fontSizes: { standard: px, fixed: Math.round((px * 13) / 16) } })
}
