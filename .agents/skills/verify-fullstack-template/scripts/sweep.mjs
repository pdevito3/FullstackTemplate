import path from 'node:path'

const configs = [
  { width: 320, height: 640, scheme: 'light', fontSize: 16 },
  { width: 320, height: 640, scheme: 'dark', fontSize: 16 },
  { width: 390, height: 844, scheme: 'light', fontSize: 16 },
  { width: 390, height: 844, scheme: 'dark', fontSize: 16 },
  { width: 1280, height: 800, scheme: 'light', fontSize: 16 },
  { width: 1280, height: 800, scheme: 'dark', fontSize: 16 },
  { width: 390, height: 844, scheme: 'light', fontSize: 24 },
  { width: 1280, height: 800, scheme: 'light', fontSize: 24 },
]
const restingConfig = { width: 1280, height: 800, scheme: null, fontSize: 16 }
const configName = ({ width, scheme, fontSize }) => `${width}px ${scheme} ${fontSize}px font`

const longName = 'Maximilianbartholomewalexandervanderwesthuizenmontgomerysmythesinclairfitzgeraldworthingtonhughes'
const longEmail = 'maximilian.bartholomew-alexander.vanderwesthuizen@long-subdomain.example-company-name.com'
const longKeys = /(^|_)(name|title|summary|description|label|note|comment|reason)$|Name$|Title$|Summary$|Description$/i
const emailKeys = /email/i

function longValues() {
  const seen = new Map()
  const longFor = (key, value) => {
    if (!seen.has(value)) seen.set(value, emailKeys.test(key) || value.includes('@') ? longEmail : longName)
    return seen.get(value)
  }
  const walk = (node) => {
    if (Array.isArray(node)) return node.map(walk)
    if (!node || typeof node !== 'object') return typeof node === 'string' && seen.has(node) ? seen.get(node) : node
    if (typeof node.type === 'string' && typeof node.value === 'string') {
      return longKeys.test(node.type) || emailKeys.test(node.type) ? { ...node, value: longFor(node.type, node.value) } : node
    }
    return Object.fromEntries(Object.entries(node).map(([key, value]) =>
      [key, typeof value === 'string' && (longKeys.test(key) || emailKeys.test(key)) ? longFor(key, value) : walk(value)]))
  }
  return walk
}

export async function routeLongValues(context, webUrl) {
  const rewrite = longValues()
  await context.route((url) => url.origin === new URL(webUrl).origin && (url.pathname.startsWith('/api/') || url.pathname === '/bff/user'),
    async (route) => {
      const response = await route.fetch()
      if (!(response.headers()['content-type'] ?? '').includes('json')) return route.fulfill({ response })
      return route.fulfill({ response, json: rewrite(await response.json()) })
    })
}

export const blockWebFonts = (context) => context.route(/\.(woff2?|ttf|otf)(\?|$)/, (route) => route.abort())

export function trackLastFocused() {
  document.addEventListener('focusin', (event) => {
    window.__sweepFocused = event.target
  }, true)
}

function readFocusLoss() {
  const el = window.__sweepFocused
  if (!el || (document.activeElement && document.activeElement !== document.body)) return null
  const reason = !el.isConnected ? 'was removed' : el.matches(':disabled') ? 'was disabled'
    : el.closest('[inert]') ? 'became inert' : !el.checkVisibility() ? 'was hidden' : null
  if (!reason) return null
  window.__sweepFocused = null
  const name = el.getAttribute('aria-label') ?? el.textContent.trim().replace(/\s+/g, ' ').slice(0, 60)
  return { element: `${el.getAttribute('role') ?? el.tagName.toLowerCase()} "${name}"`, reason }
}

function markLayoutFaults() {
  document.querySelectorAll('[data-sweep-fault]').forEach((el) => el.removeAttribute('data-sweep-fault'))
  const isHidden = (el) => {
    if (!el.checkVisibility({ opacityProperty: true, visibilityProperty: true })) return true
    for (let node = el; node; node = node.parentElement) {
      const box = node.getBoundingClientRect()
      if (getComputedStyle(node).clip !== 'auto' || (box.width <= 1 && box.height <= 1)) return true
    }
    return false
  }
  const clipsContent = (overflow) => overflow === 'hidden' || overflow === 'clip'
  const scrolls = (overflow) => overflow === 'auto' || overflow === 'scroll'
  const label = (el) => el.dataset.slot ?? el.closest('[data-slot]')?.dataset.slot ?? el.tagName.toLowerCase()
  const textOf = (el) => el.textContent.trim().replace(/\s+/g, ' ').slice(0, 80)
  const canShowFullValue = (el) => el.closest('a, button, input, select, textarea, [tabindex], [role=button], [role=link]') !== null
  const faults = []
  const add = (el, fault) => {
    el.setAttribute('data-sweep-fault', String(faults.length))
    faults.push({ ...fault, element: label(el), text: textOf(el) })
  }

  const width = document.documentElement.clientWidth
  if (document.documentElement.scrollWidth > width + 1) {
    const wide = [...document.querySelectorAll('body *')].filter((el) =>
      !isHidden(el) && el.getBoundingClientRect().right > width + 1 && (el.parentElement?.getBoundingClientRect().right ?? 0) <= width + 1)
    for (const el of wide.slice(0, 3)) {
      add(el, { kind: 'page scrolls sideways', how: `ends at ${Math.round(el.getBoundingClientRect().right)} px in a ${width} px window` })
    }
  }

  for (const el of document.querySelectorAll('body *')) {
    const text = [...el.childNodes].filter((node) => node.nodeType === Node.TEXT_NODE && node.textContent.trim())
    if (!text.length || isHidden(el)) continue
    const style = getComputedStyle(el)
    const cutX = clipsContent(style.overflowX) && el.scrollWidth > el.clientWidth + 1
    const cutY = clipsContent(style.overflowY) && el.scrollHeight > el.clientHeight + 1
    if (cutX || cutY) {
      const ellipsis = (cutX && style.textOverflow === 'ellipsis') || (cutY && style.webkitLineClamp !== 'none')
      if (!ellipsis) add(el, { kind: 'text is cut', how: 'cut with no ellipsis' })
      else if (!canShowFullValue(el)) add(el, { kind: 'text is cut', how: 'ends with an ellipsis, and no focusable element shows the full value' })
      continue
    }
    const range = document.createRange()
    range.setStartBefore(text[0])
    range.setEndAfter(text[text.length - 1])
    const shown = { ...range.getBoundingClientRect().toJSON() }
    let checkX = true
    let checkY = true
    for (let node = el.parentElement; node && node !== document.body && (checkX || checkY); node = node.parentElement) {
      const nodeStyle = getComputedStyle(node)
      const clip = node.getBoundingClientRect()
      const outX = checkX && clipsContent(nodeStyle.overflowX) && (shown.left < clip.left - 1 || shown.right > clip.right + 1)
      const outY = checkY && clipsContent(nodeStyle.overflowY) && (shown.top < clip.top - 1 || shown.bottom > clip.bottom + 1)
      if (outX) Object.assign(shown, { left: Math.max(shown.left, clip.left), right: Math.min(shown.right, clip.right) })
      if (outY) Object.assign(shown, { top: Math.max(shown.top, clip.top), bottom: Math.min(shown.bottom, clip.bottom) })
      if (shown.right <= shown.left || shown.bottom <= shown.top) break
      if (outX || outY) {
        add(el, { kind: 'text is cut', how: `runs out of the ${label(node)} that clips it` })
        break
      }
      checkX &&= !scrolls(nodeStyle.overflowX)
      checkY &&= !scrolls(nodeStyle.overflowY)
    }
  }
  return faults
}

function recordFault(faults, measured, { at, url, config }) {
  const key = [new URL(url).pathname, measured.kind, measured.element, measured.text, measured.how.replace(/\d+ px/g, '')].join('|')
  const known = faults.find((fault) => fault.key === key)
  if (known) {
    if (!known.at.includes(at)) known.at.push(at)
    if (config && !known.configs.includes(configName(config))) known.configs.push(configName(config))
    return null
  }
  const fault = { key, ...measured, url, at: [at], configs: config ? [configName(config)] : [], screenshot: `sweep-${String(faults.length + 1).padStart(2, '0')}.png` }
  faults.push(fault)
  return fault
}

export async function sweepCapture({ page, cdp, settle, outDir, capture, faults }) {
  const url = page.url()
  const scroll = await page.evaluate(() => [scrollX, scrollY])
  for (const config of configs) {
    await applyConfig(page, cdp, settle, config)
    for (const [index, measured] of (await page.evaluate(markLayoutFaults)).entries()) {
      const fault = recordFault(faults, measured, { at: capture, url, config })
      if (fault) await screenshotOf(page, `[data-sweep-fault="${index}"]`, path.join(outDir, fault.screenshot))
    }
  }
  await applyConfig(page, cdp, settle, restingConfig)
  await page.evaluate(([x, y]) => scrollTo(x, y), scroll)
}

async function screenshotOf(page, selector, file) {
  const el = page.locator(selector).first()
  // Only the window scrolls. A scrollIntoView also scrolls each overflow: hidden ancestor, and the next config
  // then measures a page that the user cannot get to.
  await el.evaluate((node) => {
    scrollTo(scrollX, node.getBoundingClientRect().top + scrollY - innerHeight / 3)
    node.style.setProperty('outline', '3px solid red', 'important')
  }).catch(() => {})
  await page.screenshot({ path: file })
  await el.evaluate((node) => node.style.removeProperty('outline')).catch(() => {})
}

async function applyConfig(page, cdp, settle, { width, height, scheme, fontSize }) {
  await page.setViewportSize({ width, height })
  await page.emulateMedia({ colorScheme: scheme })
  await cdp.send('Page.setFontSizes', { fontSizes: { standard: fontSize, fixed: Math.round(fontSize * 13 / 16) } })
  await settle()
}

// An overlay gives the focus back to its trigger after its exit animation, so the check waits for the animations.
export async function sweepFocus({ page, settle, outDir, step, faults }) {
  await settle().catch(() => {})
  const loss = await page.evaluate(readFocusLoss).catch(() => null)
  if (!loss) return
  const measured = { kind: 'focus goes to the body', element: loss.element, text: '', how: `the focused element ${loss.reason}` }
  const fault = recordFault(faults, measured, { at: step, url: page.url() })
  if (fault) await page.screenshot({ path: path.join(outDir, fault.screenshot) })
}

export function report(faults) {
  if (!faults.length) return '# Sweep\n\nNo faults.\n'
  const lines = faults.map((fault, i) => [
    `${i + 1}. **${fault.kind}** on ${new URL(fault.url).pathname}: ${fault.element}${fault.text ? ` "${fault.text}"` : ''} ${fault.how}.`,
    `   At: ${fault.at.map((at) => `\`${at}\``).join(', ')}.`,
    ...(fault.configs.length ? [`   Configs: ${fault.configs.join(', ')}.`] : []),
    `   Screenshot: [${fault.screenshot}](${fault.screenshot}).`,
  ].join('\n'))
  return `# Sweep\n\n${faults.length} fault(s).\n\n${lines.join('\n')}\n`
}
