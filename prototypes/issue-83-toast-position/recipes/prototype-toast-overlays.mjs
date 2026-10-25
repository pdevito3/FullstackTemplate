// Issue #83 prototype: does each toast position cover the buttons of an open overlay? Throwaway.
import { writeFileSync } from 'node:fs'

const variants = ['top-center', 'bottom', 'under-header']
const cases = [
  { viewport: { name: 'wide', width: 1280, height: 800 }, open: 'Open Sheet', dialog: 'Sheet Title' },
  { viewport: { name: 'wide', width: 1280, height: 800 }, open: 'Open Drawer', dialog: 'Drawer Title' },
  { viewport: { name: 'wide', width: 1280, height: 800 }, open: 'Open Dialog', dialog: 'Dialog Title' },
  { viewport: { name: 'phone', width: 390, height: 844 }, open: 'Open Dialog', dialog: 'Dialog Title' },
  { viewport: { name: 'phone', width: 390, height: 844 }, open: 'Open Sheet', dialog: 'Sheet Title' },
]
const overlap = (a, b) => a && b && a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height

export default async ({ page, step, capture, settle, expect }) => {
  const metrics = []
  for (const variant of variants) {
    for (const c of cases) {
      await step(`${variant} ${c.viewport.name} ${c.open}`, async () => {
        await page.setViewportSize({ width: c.viewport.width, height: c.viewport.height })
        await page.goto('/components')
        await page.evaluate((v) => localStorage.setItem('prototype-toast-variant', v), variant)
        await page.reload()
        await expect(page.getByRole('heading', { name: 'Component Library' })).toBeVisible()
        await page.getByRole('button', { name: c.open }).click()
        const dialog = page.getByRole('dialog', { name: c.dialog })
        await expect(dialog).toBeVisible()
        await settle()
        await page.evaluate(async () => {
          const url = performance.getEntriesByType('resource').map((e) => e.name).find((n) => n.includes('/src/components/notification.ts'))
          const { Notification } = await import(url)
          Notification.error('Something went wrong')
          Notification.success('Changes saved')
        })
        await expect(page.getByRole('alertdialog')).toHaveCount(2)
        await settle()
        await capture(`${variant}-${c.viewport.name}-${c.open.replace('Open ', '').toLowerCase()}`, { fullPage: false })
        const region = await page.getByRole('region', { name: /notification/i }).boundingBox()
        const covered = []
        for (const b of await dialog.getByRole('button').all()) {
          if (overlap(region, await b.boundingBox())) covered.push(await b.innerText() || (await b.getAttribute('aria-label')))
        }
        const title = await dialog.getByRole('heading').first().boundingBox()
        metrics.push({ variant, viewport: c.viewport.name, overlay: c.open, coveredButtons: covered, coversTitle: overlap(region, title) })
        await page.keyboard.press('Escape')
      })
    }
  }
  writeFileSync('artifacts/verify/issue82-83/prototype-83/overlays.json', JSON.stringify(metrics, null, 2))
}
