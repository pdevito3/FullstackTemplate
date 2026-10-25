// Issue #83 prototype: which interactive elements does each toast position cover, on each route? Throwaway.
import { writeFileSync } from 'node:fs'

const variants = ['top-center', 'bottom', 'under-header']
const routes = ['/', '/about', '/components', '/filter-demo']
const viewports = [
  { name: 'wide', width: 1280, height: 800 },
  { name: 'phone', width: 390, height: 844 },
]

export default async ({ page, step, settle, expect, login }) => {
  const rows = []
  const measure = async (variant, route, viewport, who) => {
    await page.setViewportSize({ width: viewport.width, height: viewport.height })
    await page.goto(route)
    await page.evaluate((v) => localStorage.setItem('prototype-toast-variant', v), variant)
    await page.reload()
    await page.waitForLoadState('networkidle')
    await settle()
    await page.evaluate(async () => {
      const url = performance.getEntriesByType('resource').map((e) => e.name).find((n) => n.includes('/src/components/notification.ts'))
      const { Notification } = await import(url)
      Notification.error('Something went wrong')
      Notification.success('Changes saved')
      Notification.info('A new version is available')
    })
    await expect(page.getByRole('alertdialog')).toHaveCount(3)
    await settle()
    const covered = await page.evaluate(() => {
      const toasts = [...document.querySelectorAll('[role="alertdialog"]')].map((t) => t.getBoundingClientRect())
      const hit = (r) => toasts.some((t) => r.width > 0 && r.left < t.right && t.left < r.right && r.top < t.bottom && t.top < r.bottom)
      return [...document.querySelectorAll('button, a[href], input, select, textarea, [role="button"], [role="combobox"], [role="tab"]')]
        .filter((el) => !el.closest('[role="region"][aria-label]') && !el.closest('.tsqd-parent-container, [class*="TanStackRouterDevtools"]'))
        .filter((el) => el.checkVisibility() && hit(el.getBoundingClientRect()))
        .map((el) => (el.getAttribute('aria-label') || el.textContent || el.getAttribute('placeholder') || el.tagName).trim().slice(0, 40))
    })
    rows.push({ variant, route, viewport: viewport.name, who, covered })
  }
  for (const who of ['anonymous', 'signed in']) {
    if (who === 'signed in') await step('sign in', async () => { await page.setViewportSize({ width: 1280, height: 800 }); await page.goto('/'); await login() })
    for (const variant of variants) {
      for (const viewport of viewports) {
        for (const route of routes) {
          await step(`${who} ${variant} ${viewport.name} ${route}`, () => measure(variant, route, viewport, who))
        }
      }
    }
  }
  writeFileSync('artifacts/verify/issue82-83/prototype-83/coverage.json', JSON.stringify(rows, null, 2))
}
