// Issue #83 prototype: the home page with three toasts, per variant. Throwaway.
export default async ({ page, step, capture, settle, expect }) => {
  for (const variant of ['bottom', 'under-header']) {
    for (const [name, width, height] of [['wide', 1280, 800], ['phone', 390, 844]]) {
      await step(`${variant} ${name} home`, async () => {
        await page.setViewportSize({ width, height })
        await page.goto('/')
        await page.evaluate((v) => localStorage.setItem('prototype-toast-variant', v), variant)
        await page.reload()
        await page.waitForLoadState('networkidle')
        await page.evaluate(async () => {
          const url = performance.getEntriesByType('resource').map((e) => e.name).find((n) => n.includes('/src/components/notification.ts'))
          const { Notification } = await import(url)
          Notification.error('Something went wrong')
          Notification.success('Changes saved')
          Notification.info('A new version is available')
        })
        await expect(page.getByRole('alertdialog')).toHaveCount(3)
        await settle()
        await capture(`${variant}-${name}-home-3`, { fullPage: false })
      })
    }
  }
}
