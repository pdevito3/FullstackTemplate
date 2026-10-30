import path from 'node:path'

const configs = [
  { width: 320, height: 640, scheme: 'light', fontSize: 16 },
  { width: 320, height: 640, scheme: 'dark', fontSize: 16 },
  { width: 390, height: 844, scheme: 'light', fontSize: 16 },
  { width: 390, height: 844, scheme: 'dark', fontSize: 16 },
  { width: 1280, height: 800, scheme: 'light', fontSize: 16 },
  { width: 1280, height: 800, scheme: 'dark', fontSize: 16 },
  { width: 320, height: 640, scheme: 'light', fontSize: 24 },
  { width: 390, height: 844, scheme: 'light', fontSize: 24 },
  { width: 1280, height: 800, scheme: 'light', fontSize: 24 },
]
const configName = ({ width, scheme, fontSize }) => `${width}px ${scheme} ${fontSize}px font`
// A new font size closes an open submenu, so the sweep of an overlay keeps the size and the font of the recipe.
const overlayConfigs = ({ width, height }) => [
  { width, height, scheme: 'light', fontSize: 16 },
  { width, height, scheme: 'dark', fontSize: 16 },
]

const longName = 'Maximilianbartholomewalexandervanderwesthuizenmontgomerysmythesinclairfitzgeraldworthingtonhughesson'
const longEmail = 'maximilian.bartholomew-alexander.vanderwesthuizen@long-subdomain.example-company-name.com'
const nameKeys = new Set(['name', 'title', 'summary', 'description', 'label', 'firstName', 'lastName', 'fullName', 'tenantName',
  'requestedBy', 'given_name', 'family_name'])
const emailKeys = new Set(['email'])

function longValues() {
  const seen = new Map()
  const longFor = (key, value) => {
    const seenKey = `${emailKeys.has(key) ? 'email' : 'name'}|${value}`
    if (!seen.has(seenKey)) {
      const id = String(seen.size).padStart(3, '0')
      seen.set(seenKey, emailKeys.has(key) ? longEmail.replace('@', `${id}@`) : longName.slice(0, -id.length) + id)
    }
    return seen.get(seenKey)
  }
  const rewrites = (key, value) => typeof value === 'string' && value !== '' && (nameKeys.has(key) || emailKeys.has(key))
  const walk = (node) => {
    if (Array.isArray(node)) return node.map(walk)
    if (!node || typeof node !== 'object') return node
    if (typeof node.type === 'string' && 'value' in node) {
      return rewrites(node.type, node.value) ? { ...node, value: longFor(node.type, node.value) } : node
    }
    return Object.fromEntries(Object.entries(node).map(([key, value]) => [key, rewrites(key, value) ? longFor(key, value) : walk(value)]))
  }
  return walk
}

export async function routeLongValues(context, webUrl) {
  const rewrite = longValues()
  await context.route((url) => url.origin === new URL(webUrl).origin && (url.pathname.startsWith('/api/') || url.pathname === '/bff/user'),
    async (route) => {
      const response = await route.fetch().catch(() => null)
      if (!response) return route.abort().catch(() => {})
      const { 'content-length': _, ...headers } = response.headers()
      if (!(headers['content-type'] ?? '').includes('json')) return route.fulfill({ response })
      const json = await response.json().catch(() => undefined)
      if (json === undefined) return route.fulfill({ response })
      return route.fulfill({ response, headers, json: rewrite(json) })
    })
}

export const blockWebFonts = (context) => context.route(/\.(woff2?|ttf|otf)(\?|$)/, (route) => route.abort())

export function trackLastFocused() {
  document.addEventListener('focusin', (event) => {
    window.__sweepFocused = event.target
  }, true)
}

// React Aria gives the focus back in the animation frame after an overlay unmounts, so for one frame it is on the body.
// The check waits two frames, because a callback of the first frame can run before the callback of React Aria.
// A hidden headed window runs no frames, so the wait also ends after 100 ms.
async function readFocusLoss() {
  await new Promise((resolve) => {
    requestAnimationFrame(() => requestAnimationFrame(resolve))
    setTimeout(resolve, 100)
  })
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
    while (el.parentElement && getComputedStyle(el).display === 'contents') el = el.parentElement
    if (!el.checkVisibility({ opacityProperty: true, visibilityProperty: true })) return true
    for (let node = el; node; node = node.parentElement) {
      if (getComputedStyle(node).display === 'contents') continue
      const box = node.getBoundingClientRect()
      if (getComputedStyle(node).clip !== 'auto' || (box.width <= 1 && box.height <= 1)) return true
    }
    return false
  }
  const clipsContent = (overflow) => overflow === 'hidden' || overflow === 'clip'
  const scrolls = (overflow) => overflow === 'auto' || overflow === 'scroll'
  const label = (el) => el.dataset.slot ?? el.closest('[data-slot]')?.dataset.slot ?? el.tagName.toLowerCase()
  const textOf = (el) => el.textContent.trim().replace(/\s+/g, ' ').slice(0, 80)
  const oneLine = (value) => (value ?? '').trim().replace(/\s+/g, ' ')
  // The sweep cannot tell whether a press shows the full value. It only finds that the title or the name holds it.
  const canShowFullValue = (el) => {
    const host = el.closest('a[href], button, input, select, textarea, summary, [tabindex]')
    if (!host || host.matches(':disabled, [aria-disabled="true"]')) return false
    // A roving tabindex gives -1 to each item of a composite widget but the current one. The arrow keys focus the others.
    if (host.tabIndex < 0 && !host.closest('[role=tablist], [role=menu], [role=listbox], [role=grid], [role=toolbar], [role=tree]')) return false
    const full = oneLine(el.textContent)
    const labelledBy = host.getAttribute('aria-labelledby')?.split(/\s+/).map((id) => document.getElementById(id)?.textContent).join(' ')
    const name = host.getAttribute('aria-label') ?? labelledBy ?? host.textContent
    return [host.getAttribute('title'), name].some((value) => oneLine(value).includes(full))
  }
  const ownText = (el) => [...el.childNodes].filter((node) => node.nodeType === Node.TEXT_NODE && node.textContent.trim())
  const textRect = (text) => {
    const range = document.createRange()
    range.setStartBefore(text[0])
    range.setEndAfter(text[text.length - 1])
    return range.getBoundingClientRect()
  }
  const faults = []
  const add = (el, fault) => {
    el.setAttribute('data-sweep-fault', `${el.getAttribute('data-sweep-fault') ?? ''} ${faults.length}`.trim())
    faults.push({ element: label(el), text: textOf(el), ...fault })
  }

  const width = document.documentElement.clientWidth
  const overlays = '[role=dialog], [role=alertdialog], [role=menu], [role=listbox], [role=tooltip], [data-slot=popover-content]'
  for (const el of document.querySelectorAll(overlays)) {
    if (isHidden(el) || el.parentElement?.closest(overlays)) continue
    const box = el.getBoundingClientRect()
    if (box.left < -1 || box.right > width + 1) {
      add(el, { kind: 'overlay goes past the window', how: `spans ${Math.round(box.left)} to ${Math.round(box.right)} px in a ${width} px window` })
    }
  }

  // React Aria sets an inline max-height from the space on the page, and an inline style wins over a class.
  for (const el of document.querySelectorAll('[style*="max-height"]')) {
    if (isHidden(el)) continue
    const inline = [el.style.getPropertyValue('max-height'), el.style.getPropertyPriority('max-height')]
    el.style.removeProperty('max-height')
    const classMax = getComputedStyle(el).maxHeight
    el.style.setProperty('max-height', ...inline)
    if (!classMax.endsWith('px')) continue
    const height = el.getBoundingClientRect().height
    if (height > parseFloat(classMax) + 1) {
      add(el, { kind: 'list grows past its max height', how: `is ${Math.round(height)} px high, and its class sets a max height of ${Math.round(parseFloat(classMax))} px` })
    }
  }

  const hasFixedHeight = (el) => {
    const height = el.getBoundingClientRect().height
    const inline = [el.style.getPropertyValue('height'), el.style.getPropertyPriority('height')]
    el.style.setProperty('height', 'auto', 'important')
    const contentHeight = el.getBoundingClientRect().height
    el.style.removeProperty('height')
    if (inline[0]) el.style.setProperty('height', ...inline)
    return Math.abs(contentHeight - height) > 1
  }
  for (const bar of document.querySelectorAll('header, [role=toolbar]')) {
    if (isHidden(bar) || !hasFixedHeight(bar)) continue
    const walker = document.createTreeWalker(bar, NodeFilter.SHOW_TEXT)
    const rects = []
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if (!node.textContent.trim() || isHidden(node.parentElement)) continue
      const range = document.createRange()
      range.selectNodeContents(node)
      rects.push(...[...range.getClientRects()].filter((rect) => rect.width > 0 && rect.height > 0))
    }
    let lines = 0
    let lineBottom = -Infinity
    for (const rect of rects.sort((a, b) => a.top - b.top)) {
      if (rect.top >= lineBottom - 1) lines++
      lineBottom = Math.max(lineBottom, rect.bottom)
    }
    const box = bar.getBoundingClientRect()
    const element = bar.dataset.slot ?? bar.getAttribute('role') ?? bar.tagName.toLowerCase()
    if (lines > 1) add(bar, { kind: 'fixed-height bar wraps', element, how: `has its text on ${lines} lines` })
    else if (rects.some((rect) => rect.top < box.top - 1 || rect.bottom > box.bottom + 1)) {
      add(bar, { kind: 'fixed-height bar wraps', element, how: 'has text that runs out of its height' })
    }
  }

  if (document.documentElement.scrollWidth > width + 1) {
    const past = (right) => right > width + 1
    const clippedInside = (node) => {
      for (; node && node !== document.body; node = node.parentElement) {
        if (getComputedStyle(node).overflowX !== 'visible' && !past(node.getBoundingClientRect().right)) return true
      }
      return false
    }
    const wide = [...document.querySelectorAll('body *')].filter((el) => {
      if (isHidden(el)) return false
      const right = el.getBoundingClientRect().right
      const text = ownText(el)
      if (past(right)) return !past(el.parentElement?.getBoundingClientRect().right ?? 0) && !clippedInside(el.parentElement)
      return text.length > 0 && past(textRect(text).right) && !clippedInside(el)
    })
    for (const el of wide.slice(0, 3)) {
      const right = Math.max(el.getBoundingClientRect().right, ownText(el).length ? textRect(ownText(el)).right : 0)
      add(el, { kind: 'page scrolls sideways', how: `ends at ${Math.round(right)} px in a ${width} px window` })
    }
    if (!wide.length) {
      add(document.body, { kind: 'page scrolls sideways', element: 'page', text: '', how: `is ${document.documentElement.scrollWidth} px wide in a ${width} px window` })
    }
  }

  for (const el of document.querySelectorAll('body *')) {
    const text = ownText(el)
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
    const shown = { ...textRect(text).toJSON() }
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

  // A heading shows its overflow, so a word that is wider than the heading runs over the gutter but can stay inside the window.
  // Each text node counts only up to the edges of a box inside the heading that clips it, for example a title that truncates.
  const shownTextOf = (heading) => {
    const shown = { left: Infinity, right: -Infinity }
    const walker = document.createTreeWalker(heading, NodeFilter.SHOW_TEXT)
    for (let node = walker.nextNode(); node; node = walker.nextNode()) {
      if (!node.textContent.trim() || isHidden(node.parentElement)) continue
      const range = document.createRange()
      range.selectNodeContents(node)
      let { left, right } = range.getBoundingClientRect()
      for (let box = node.parentElement; box !== heading; box = box.parentElement) {
        if (!clipsContent(getComputedStyle(box).overflowX)) continue
        const clip = box.getBoundingClientRect()
        left = Math.max(left, clip.left)
        right = Math.min(right, clip.right)
      }
      if (right <= left) continue
      shown.left = Math.min(shown.left, left)
      shown.right = Math.max(shown.right, right)
    }
    return shown
  }
  for (const heading of document.querySelectorAll('h1, h2, h3, h4, h5, h6, [role=heading]')) {
    if (!heading.offsetWidth || isHidden(heading)) continue
    const style = getComputedStyle(heading)
    if (style.display === 'inline' || style.overflowX !== 'visible') continue
    const rect = heading.getBoundingClientRect()
    const scale = rect.width / heading.offsetWidth
    const left = rect.left + (heading.clientLeft + parseFloat(style.paddingLeft)) * scale
    const right = left + (heading.clientWidth - parseFloat(style.paddingLeft) - parseFloat(style.paddingRight)) * scale
    const shown = shownTextOf(heading)
    const overrun = Math.max(0, shown.right - right) + Math.max(0, left - shown.left)
    if (overrun > 1) {
      add(heading, { kind: 'heading text runs past its box', element: heading.dataset.slot ?? heading.tagName.toLowerCase(),
        how: `runs ${Math.round(overrun)} px past the ${Math.round(right - left)} px of its box` })
    }
  }
  return faults
}

function recordFault(faults, measured, { at, url, config }) {
  const key = [new URL(url).pathname, measured.kind, measured.element, measured.text, measured.how.replace(/-?\d+/g, '')].join('|')
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

// A resize can cross a breakpoint and remount the element that has the focus. The sweep gives the focus back,
// or forgets the last focused element, so that the next focus check does not report a loss that the sweep caused.
// A loss from before the capture stays for the focus check at the end of the step.
// A capture that holds an open overlay is swept at the size and the font of the recipe, and its screenshots do not scroll.
export async function sweepCapture({ page, cdp, settle, outDir, capture, faults, holdsOverlay }) {
  const url = page.url()
  const resting = { ...page.viewportSize(), scheme: 'light', fontSize: 16 }
  const scroll = await page.evaluate(() => [scrollX, scrollY])
  const focused = await page.evaluateHandle(() => document.activeElement)
  for (const config of holdsOverlay ? overlayConfigs(resting) : configs) {
    await applyConfig(page, cdp, settle, config)
    for (const [index, measured] of (await page.evaluate(markLayoutFaults)).entries()) {
      const fault = recordFault(faults, measured, { at: capture, url, config })
      if (fault) await screenshotOf(page, `[data-sweep-fault~="${index}"]`, path.join(outDir, fault.screenshot), { scroll: !holdsOverlay })
    }
  }
  await page.evaluate(() => document.querySelectorAll('[data-sweep-fault]').forEach((el) => el.removeAttribute('data-sweep-fault')))
  await applyConfig(page, cdp, settle, resting)
  await page.evaluate(([x, y]) => scrollTo(x, y), scroll)
  await focused.evaluate((el) => {
    if (!el || el === document.body) return
    if (el.isConnected) el.focus({ preventScroll: true })
    window.__sweepFocused = document.activeElement === document.body ? null : document.activeElement
  })
  await focused.dispose()
}

async function screenshotOf(page, selector, file, { scroll }) {
  const el = page.locator(selector).first()
  // Only the window scrolls. A scrollIntoView also scrolls each overflow: hidden ancestor, and the next config
  // then measures a page that the user cannot get to.
  await el.evaluate((node, scroll) => {
    if (scroll) scrollTo(scrollX, node.getBoundingClientRect().top + scrollY - innerHeight / 3)
    node.style.setProperty('outline', '3px solid red', 'important')
  }, scroll, { timeout: 1000 }).catch(() => {})
  await page.screenshot({ path: file })
  await el.evaluate((node) => node.style.removeProperty('outline'), null, { timeout: 1000 }).catch(() => {})
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
  if (fault) await page.screenshot({ path: path.join(outDir, fault.screenshot) }).catch(() => {})
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
