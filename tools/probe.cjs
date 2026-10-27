// Usage: node probe.cjs <baseURL> <outDir>
// Reproduces issue 145 in Chromium against the Vite dev server with a mocked BFF, measures the WCAG contrast of the
// destructive color on every surface, and takes the screenshots for the main/head comparison.
const path = require('node:path')
const fs = require('node:fs')
const { chromium } = require('/Users/paul/repos/FullstackTemplate-worktrees/issue-145/frontend/node_modules/@playwright/test')

const [baseURL, outDir] = process.argv.slice(2)
fs.mkdirSync(path.join(outDir, 'shots'), { recursive: true })
const results = { contrast: [], shortYear: [] }

async function newPage(browser, theme, width, height = 900) {
  const context = await browser.newContext({
    viewport: { width, height },
    locale: 'en-US',
    timezoneId: 'UTC',
    reducedMotion: 'reduce',
    colorScheme: theme,
  })
  await context.addInitScript((t) => localStorage.setItem('ui-theme', t), theme)
  await context.route((url) => url.pathname === '/bff/user', (r) => r.fulfill({ status: 204 }))
  await context.route((url) => url.pathname.startsWith('/api/'), (r) => {
    console.error('unmocked', r.request().url())
    return r.fulfill({ status: 404 })
  })
  const page = await context.newPage()
  await page.clock.setFixedTime(new Date('2026-08-15T12:00:00Z'))
  return page
}

// The text color and the composited background of an element, as painted sRGB, and their WCAG ratio.
async function contrastOf(locator) {
  return locator.evaluate((el) => {
    const canvas = document.createElement('canvas')
    canvas.width = canvas.height = 1
    const ctx = canvas.getContext('2d', { willReadFrequently: true })
    const rgba = (css) => {
      ctx.clearRect(0, 0, 1, 1)
      ctx.fillStyle = '#000'
      ctx.fillStyle = css
      ctx.fillRect(0, 0, 1, 1)
      const [r, g, b, a] = ctx.getImageData(0, 0, 1, 1).data
      return [r, g, b, a / 255]
    }
    const over = (top, bottom) => top.slice(0, 3).map((c, i) => c * top[3] + bottom[i] * (1 - top[3])).concat(1)
    const layers = []
    for (let n = el; n; n = n.parentElement) {
      const c = rgba(getComputedStyle(n).backgroundColor)
      if (c[3] > 0) layers.push(c)
      if (c[3] === 1) break
    }
    let bg = [255, 255, 255, 1]
    for (const layer of layers.reverse()) bg = over(layer, bg)
    const fg = over(rgba(getComputedStyle(el).color), bg)
    const lum = ([r, g, b]) =>
      [r, g, b]
        .map((v) => v / 255)
        .map((v) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4))
        .reduce((s, v, i) => s + v * [0.2126, 0.7152, 0.0722][i], 0)
    const [hi, lo] = [lum(fg), lum(bg)].sort((a, b) => b - a)
    const hex = (c) => '#' + c.slice(0, 3).map((v) => Math.round(v).toString(16).padStart(2, '0')).join('')
    return { fg: hex(fg), bg: hex(bg), ratio: Math.round(((hi + 0.05) / (lo + 0.05)) * 100) / 100 }
  })
}

async function openDateControl(page) {
  await page.goto(baseURL + '/filter-demo')
  await page.getByRole('heading', { name: 'Filter Builder Demo' }).waitFor()
  await page.getByRole('button', { name: 'Add Filter', exact: true }).click()
  await page.getByRole('menuitem', { name: 'Created Date', exact: true }).click()
  const control = page.getByRole('dialog', { name: 'Created Date filter', exact: true })
  await control.waitFor()
  return control
}

async function typeDate(page, control, field, digits) {
  await control.getByRole('spinbutton', { name: `month, ${field}`, exact: true }).click()
  await page.keyboard.type(digits)
}

async function describe(control, field) {
  const segment = control.getByRole('spinbutton', { name: `year, ${field}`, exact: true })
  return {
    field,
    text: await control.getByRole('group', { name: field, exact: true }).textContent(),
    ariaInvalid: await segment.getAttribute('aria-invalid'),
    description: await segment.evaluate((el) => {
      const ids = (el.getAttribute('aria-describedby') ?? '').split(/\s+/).filter(Boolean)
      return ids.map((id) => document.getElementById(id)?.textContent ?? '').join(' ').trim()
    }),
  }
}

async function measure(browser, theme) {
  const page = await newPage(browser, theme, 1280)
  const control = await openDateControl(page)
  await control.getByRole('button', { name: 'Between', exact: true }).click()
  await typeDate(page, control, 'Start date', '03102026')
  await typeDate(page, control, 'End date', '03012026')
  const add = async (surface, locator) => results.contrast.push({ theme, surface, ...(await contrastOf(locator)) })
  await add('invalid segment, not focused', control.getByRole('spinbutton', { name: 'month, End date', exact: true }))
  await add('invalid segment, focused', control.getByRole('spinbutton', { name: 'year, End date', exact: true }))
  await add('range error message', control.getByText('The end date is before the start date.'))
  await control.screenshot({ path: path.join(outDir, `invalid-range-${theme}.png`) })

  await page.goto(baseURL + '/components')
  await page.getByRole('button', { name: 'Destructive' }).waitFor()
  await add('destructive button', page.getByRole('button', { name: 'Destructive' }))
  await add('destructive badge', page.getByText('Destructive', { exact: true }).nth(1))
  await page.close()
}

async function shortYear(browser, theme) {
  const page = await newPage(browser, theme, 1280)
  for (const mode of ['On', 'Before', 'After']) {
    const control = await openDateControl(page)
    await control.getByRole('button', { name: mode, exact: true }).click()
    await typeDate(page, control, 'Date', '0310999')
    await page.keyboard.press('Tab')
    results.shortYear.push({ theme, mode, ...(await describe(control, 'Date')) })
    if (mode === 'On') {
      await control.screenshot({ path: path.join(outDir, `short-year-on-${theme}.png`) })
      await typeDate(page, control, 'Date', '03102026')
      await page.keyboard.press('Tab')
      results.shortYear.push({ theme, mode: 'On, after the fix', ...(await describe(control, 'Date')) })
    }
    await page.keyboard.press('Escape')
  }
  const control = await openDateControl(page)
  await control.getByRole('button', { name: 'Between', exact: true }).click()
  await typeDate(page, control, 'Start date', '0310999')
  await page.keyboard.press('Tab')
  await typeDate(page, control, 'End date', '0320202612')
  await page.keyboard.press('Tab')
  for (const field of ['Start date', 'End date']) results.shortYear.push({ theme, mode: 'Between', ...(await describe(control, field)) })
  await control.screenshot({ path: path.join(outDir, `short-year-between-${theme}.png`) })
  await page.close()
}

async function shots(browser) {
  for (const theme of ['light', 'dark'])
    for (const width of [320, 768, 1280])
      for (const route of ['filter-demo', 'components']) {
        const page = await newPage(browser, theme, width)
        await page.goto(`${baseURL}/${route}`)
        await page.waitForLoadState('networkidle')
        await page.evaluate(() => document.fonts.ready)
        await page.screenshot({ path: path.join(outDir, 'shots', `${route}-${width}-${theme}.png`), fullPage: true, animations: 'disabled' })
        await page.close()
      }
}

;(async () => {
  const browser = await chromium.launch()
  try {
    for (const theme of ['light', 'dark']) {
      await measure(browser, theme)
      await shortYear(browser, theme)
    }
    await shots(browser)
  } finally {
    await browser.close()
  }
  fs.writeFileSync(path.join(outDir, 'results.json'), JSON.stringify(results, null, 2))
  console.table(results.contrast)
  console.table(results.shortYear)
})()
