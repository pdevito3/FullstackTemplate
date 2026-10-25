import type { Locator } from '@playwright/test'

// A visually hidden element (Tailwind sr-only) is 1 px by 1 px, and Playwright counts it as visible.
const visuallyHiddenSizePx = 1

export async function takesUpSpace(element: Locator): Promise<boolean> {
  const box = await element.boundingBox()
  return box !== null && box.width > visuallyHiddenSizePx && box.height > visuallyHiddenSizePx
}
