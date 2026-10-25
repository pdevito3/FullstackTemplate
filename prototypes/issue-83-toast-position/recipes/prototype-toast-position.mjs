// Issue #83 prototype: compare the toast region positions in the real app. Throwaway.
import { writeFileSync } from 'node:fs'

const variants = ['top-center', 'bottom', 'under-header']
const viewports = [
  { name: 'wide', width: 1280, height: 800 },
  { name: 'phone', width: 390, height: 844 },
]
const themes = ['light', 'dark']
const messages = ['Success', 'Error', 'Info']

const overlap = (a, b) => a && b && a.x < b.x + b.width && b.x < a.x + a.width && a.y < b.y + b.height && b.y < a.y + a.height

export default async ({ page, step, capture, settle, expect }) => {
  const metrics = []
  for (const variant of variants) {
    for (const viewport of viewports) {
      for (const theme of themes) {
        await step(`${variant} ${viewport.name} ${theme}`, async () => {
          await page.setViewportSize({ width: viewport.width, height: viewport.height })
          await page.goto('/components')
          await page.evaluate(
            ([v, t]) => {
              localStorage.setItem('prototype-toast-variant', v)
              localStorage.setItem('ui-theme', t)
            },
            [variant, theme],
          )
          await page.reload()
          await expect(page.getByRole('heading', { name: 'Component Library' })).toBeVisible()
          await settle()
          const press = (name) =>
            page.evaluate(async (n) => {
              const button = [...document.querySelectorAll('button')].find((b) => b.textContent?.trim() === n)
              button.click()
              for (let i = 0; i < 30; i++) {
                await new Promise(requestAnimationFrame)
                const enter = document
                  .getAnimations()
                  .find((a) => a.effect?.pseudoElement?.startsWith('::view-transition-new') && !a.effect.pseudoElement.includes('(root)'))
                if (enter) return { name: enter.animationName, keyframes: enter.effect.getKeyframes().map((k) => k.transform) }
              }
              return null
            }, name)
          const enter = await press(messages[0])
          await expect(page.getByRole('alertdialog')).toHaveCount(1)
          await settle()
          await capture(`${variant}-${viewport.name}-${theme}-1`, { fullPage: false })
          await press(messages[1])
          await press(messages[2])
          await expect(page.getByRole('alertdialog')).toHaveCount(3)
          await settle()
          await capture(`${variant}-${viewport.name}-${theme}-3`, { fullPage: false })
          const region = await page.getByRole('region', { name: /notification/i }).boundingBox()
          const header = await page.locator('header').first().boundingBox()
          const trigger = await page.getByRole('button', { name: 'Toggle Sidebar' }).first().boundingBox()
          const breadcrumb = await page.getByRole('navigation', { name: 'breadcrumb', exact: true }).boundingBox()
          const order = await page.getByRole('alertdialog').evaluateAll((els) =>
            els.map((el) => ({ text: el.textContent, top: Math.round(el.getBoundingClientRect().top) })).sort((a, b) => a.top - b.top).map((t) => t.text),
          )
          metrics.push({
            variant,
            viewport: viewport.name,
            theme,
            region,
            coversHeader: overlap(region, header),
            coversSidebarTrigger: overlap(region, trigger),
            coversBreadcrumb: overlap(region, breadcrumb),
            topToBottomOrder: order,
            enter,
          })
        })
      }
    }
  }
  writeFileSync('artifacts/verify/issue82-83/prototype-83/metrics.json', JSON.stringify(metrics, null, 2))
}
