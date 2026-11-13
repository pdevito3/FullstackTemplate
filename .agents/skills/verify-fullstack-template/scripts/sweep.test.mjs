import assert from 'node:assert/strict'
import { mkdtempSync, rmSync } from 'node:fs'
import { createRequire } from 'node:module'
import { tmpdir } from 'node:os'
import path from 'node:path'
import { after, before, test } from 'node:test'
import { markLayoutFaults, sweepFocus, trackLastFocused } from './sweep.mjs'

const requireFromFrontend = createRequire(new URL('../../../../frontend/package.json', import.meta.url))
const { chromium } = requireFromFrontend('@playwright/test')

const word = 'Maximilianbartholomewalexandervanderwesthuizenmontgomerysmythesinclairfitzgeraldworthington'
const words = 'Some words that take many lines in a narrow box, so that a line clamp has text to hide after the second line of the box.'
const truncate = 'display:block;width:100px;overflow:hidden;white-space:nowrap;text-overflow:ellipsis'
const lineClamp = 'display:-webkit-box;-webkit-box-orient:vertical;-webkit-line-clamp:2;width:100px;overflow:hidden'

const faults = [
  {
    name: 'a box that hides its overflow, with no ellipsis',
    html: `<div data-slot="cell" style="width:100px;overflow:hidden;white-space:nowrap">${word}</div>`,
    kind: 'text is cut',
    element: 'cell',
  },
  {
    name: 'a parent that hides the overflow of its child, with no ellipsis',
    html: `<div data-slot="cell" style="width:100px;overflow:hidden"><span style="white-space:nowrap">${word}</span></div>`,
    kind: 'text is cut',
    element: 'cell',
  },
  {
    name: 'a toast: a long word runs past its box, over the close button, and off the window',
    html: `<div style="position:fixed;right:8px;bottom:8px;width:300px;display:flex">
      <div style="flex:1;min-width:0"><span data-slot="toast-title" style="display:block">${word}</span></div><button>x</button></div>`,
    kind: 'text runs past its box',
    element: 'toast-title',
  },
  {
    name: 'a picker option: a long word runs past the option in a list that scrolls only by accident',
    html: `<div style="width:200px;overflow:hidden"><div style="max-height:200px;overflow-y:auto">
      <div data-slot="option" style="display:flex;width:100%">${word}</div></div></div>`,
    kind: 'text runs past its box',
    element: 'option',
  },
  {
    name: 'a word that does not wrap and runs past its box, with no box that scrolls to it',
    html: `<div style="width:100px"><span data-slot="badge" style="white-space:nowrap">${word}</span></div>`,
    kind: 'text runs past its box',
    element: 'badge',
  },
  {
    name: 'a bar that is fixed past the edge of the window',
    html: `<div data-slot="bar" style="position:fixed;left:0;top:0;width:900px">${word}</div>`,
    kind: 'text is cut',
    element: 'bar',
  },
  {
    name: 'an ellipsis on a flex box, which never shows it',
    html: `<a href="/" data-slot="link" style="display:flex;width:100px;overflow:hidden;white-space:nowrap;text-overflow:ellipsis">${word}</a>`,
    kind: 'text is cut',
    element: 'link',
  },
  {
    name: 'an ellipsis on a parent, which does not show it for a block child',
    html: `<a href="/" data-slot="link" style="${truncate}"><div style="white-space:nowrap">${word}</div></a>`,
    kind: 'text is cut',
    element: 'link',
  },
  {
    name: 'an ellipsis with nothing that shows the full value',
    html: `<p data-slot="name" style="${truncate}">${word}</p>`,
    kind: 'text is cut',
    element: 'name',
  },
  {
    name: 'an ellipsis on a parent, with nothing that shows the full value',
    html: `<p data-slot="name" style="${truncate}"><span>${word}</span></p>`,
    kind: 'text is cut',
    element: 'name',
  },
  {
    name: 'an ellipsis on a parent that cuts a text after a link',
    html: `<div data-slot="row" style="${truncate}"><a href="/">${word}</a> <span>Overdue</span></div>`,
    kind: 'text is cut',
    element: 'row',
  },
  {
    name: 'a line clamp on a parent, with nothing that shows the full value',
    html: `<p data-slot="name" style="${lineClamp}"><span>${words}</span></p>`,
    kind: 'text is cut',
    element: 'name',
  },
  {
    name: 'text in a flex box that hides its overflow and is cut at the left',
    html: `<div data-slot="row" style="display:flex;justify-content:flex-end;width:100px;overflow:hidden">${word}</div>`,
    kind: 'text is cut',
    element: 'row',
  },
  {
    name: 'text that wraps past a fixed height and prints over the next row',
    html: `<p data-slot="note" style="width:100px;height:20px;margin:0">${words}</p><p>Next row</p>`,
    kind: 'text runs past its box',
    element: 'note',
  },
  {
    name: 'one line that runs more than half its font size below a fixed height',
    html: `<p data-slot="note" style="width:100px;height:10px;margin:0;font-size:16px;line-height:22px">Text</p>`,
    kind: 'text runs past its box',
    element: 'note',
  },
  {
    name: 'a line clamp with nothing that shows the full value',
    html: `<p data-slot="name" style="${lineClamp}">${words}</p>`,
    kind: 'text is cut',
    element: 'name',
  },
  {
    name: 'a line clamp that cuts a long word at the side, where it shows no ellipsis',
    html: `<a href="/" data-slot="link" style="${lineClamp}">${word}</a>`,
    kind: 'text is cut',
    element: 'link',
  },
  {
    name: 'a picker option: the text is in a span, which grows with the word and holds it',
    html: `<div style="width:200px;overflow:hidden"><div style="max-height:200px;overflow-y:auto">
      <div data-slot="option" style="display:flex;width:100%"><span>${word}</span></div></div></div>`,
    kind: 'text runs past its box',
    element: 'option',
  },
  {
    name: 'a flex row in a dialog body: the name grows over the button',
    html: `<div style="position:fixed;left:20px;top:20px;width:300px;max-height:400px;overflow-y:auto">
      <div style="display:flex;gap:8px"><span data-slot="name">${word}</span><button>Remove</button></div></div>`,
    kind: 'text runs past its box',
    element: 'name',
  },
  {
    name: 'a word that does not wrap in a box narrower than the box that scrolls',
    html: `<div style="width:300px;overflow-y:auto"><div style="width:100px"><span data-slot="badge" style="white-space:nowrap">${word}</span></div></div>`,
    kind: 'text runs past its box',
    element: 'badge',
  },
  {
    name: 'a long word in pre-wrap in a box that scrolls',
    html: `<div style="width:100px;overflow-y:auto"><p data-slot="note" style="white-space:pre-wrap">${word}</p></div>`,
    kind: 'text runs past its box',
    element: 'note',
  },
  {
    name: 'a clamp on a block box, which cuts a line with no ellipsis',
    html: `<a href="/" data-slot="link" style="display:block;-webkit-line-clamp:2;width:100px;height:40px;overflow:hidden">${words}</a>`,
    kind: 'text is cut',
    element: 'link',
  },
  {
    name: 'an ellipsis on a -webkit-box with no clamp, which never shows it',
    html: `<a href="/" data-slot="link" style="display:-webkit-box;width:100px;overflow:hidden;white-space:nowrap;text-overflow:ellipsis">${word}</a>`,
    kind: 'text is cut',
    element: 'link',
  },
  {
    name: 'overflow on an inline span, which does nothing',
    html: `<div data-slot="row" style="width:100px"><span style="overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${word}</span></div>`,
    kind: 'text runs past its box',
    element: 'row',
  },
  {
    name: 'a heading that grows with its word past its parent',
    html: `<div style="width:300px"><div style="display:flex;flex-wrap:wrap;gap:16px">
      <h1 data-slot="title" style="margin:0">${word}</h1><button>New</button></div></div>`,
    kind: 'text runs past its box',
    element: 'title',
  },
  {
    name: 'a line clamp that also cuts a long word at the side',
    html: `<a href="/" data-slot="link" style="${lineClamp}">${word} ${words}</a>`,
    kind: 'text is cut',
    element: 'link',
  },
  {
    name: 'a dialog title: a long word runs past the heading',
    html: `<h2 data-slot="dialog-title" style="width:100px">${word}</h2>`,
    kind: 'heading text runs past its box',
    element: 'dialog-title',
  },
]

const planned = [
  { name: 'an ellipsis in a link', html: `<a href="/" style="${truncate}">${word}</a>` },
  { name: 'an ellipsis on a parent, with the text in an inline child', html: `<a href="/" style="${truncate}"><span>${word}</span></a>` },
  { name: 'an ellipsis in a button that has the full value as its name', html: `<button aria-label="Open ${word}"><span style="${truncate}">${word}</span></button>` },
  { name: 'an ellipsis on a parent, with the text in a link', html: `<div style="${truncate}"><a href="/">${word}</a></div>` },
  { name: 'a line clamp in a link', html: `<a href="/" style="${lineClamp}">${words}</a>` },
  { name: 'a line clamp on a parent, with the text in an inline child', html: `<a href="/" style="${lineClamp}"><span>${words}</span></a>` },
  { name: 'a line clamp on a parent, with the text in a block child', html: `<a href="/" style="${lineClamp}"><p>${words}</p></a>` },
  { name: 'a text in the padding of its button', html: `<button style="display:inline-flex;justify-content:center;box-sizing:border-box;width:76px;padding:0 12px;white-space:nowrap;border:1px solid">Tomorrow</button>` },
  { name: 'prose in pre-wrap, where a space hangs at the end of a line', html: `<div style="width:80px;white-space:pre-wrap">${words}</div>` },
  { name: 'a popover that is wider than the parent it is anchored to', html: `<div style="position:relative;width:40px"><div style="position:absolute;width:200px">Some words for the popover</div></div>` },
  { name: 'a long word that wraps', html: `<div style="width:100px;overflow-wrap:anywhere">${word}</div>` },
  { name: 'a wide table in a box that scrolls sideways', html: `<div style="width:100px;overflow-x:auto"><table><tr><td style="white-space:nowrap">${word}</td></tr></table></div>` },
  { name: 'a line of code that scrolls sideways', html: `<pre style="width:100px;overflow-x:auto"><code>${word}</code></pre>` },
  { name: 'a line of code in a padded box that scrolls', html: `<div style="width:100px;padding:16px;overflow:auto"><pre>${word}</pre></div>` },
  { name: 'a line of code in a padded wrapper in a box that scrolls', html: `<div style="width:300px;overflow:auto"><div style="padding:16px"><pre>${word}</pre></div></div>` },
  { name: 'a line of code with a margin in a box that scrolls', html: `<div style="width:300px;overflow:auto"><pre style="margin:0 16px">${word}</pre></div>` },
  { name: 'a list item that does not wrap in a box that scrolls', html: `<div style="width:300px;overflow-x:auto"><ul style="padding-left:40px"><li style="white-space:nowrap">${word}</li></ul></div>` },
  { name: 'a line of code in a scaled box that scrolls', html: `<div style="transform:scale(.5);transform-origin:0 0"><div style="width:300px;overflow:auto"><pre>${word}</pre></div></div>` },
  { name: 'a wide table under a wrapper in a box that scrolls', html: `<div style="width:300px;overflow-x:auto"><div style="width:100%"><table style="min-width:800px"><tr><td>${words}</td></tr></table></div></div>` },
  { name: 'a line that does not wrap in a box that scrolls sideways', html: `<div style="width:100px;overflow-x:auto"><p style="white-space:nowrap">${word}</p></div>` },
  { name: 'an inline child that does not wrap in a box that scrolls sideways', html: `<div style="width:100px;overflow-x:auto"><p><span style="white-space:nowrap">${word}</span></p></div>` },
  { name: 'a separator that hangs past its box', html: `<div style="width:4px"><span style="white-space:nowrap;padding:0 6px">|</span></div>` },
  { name: 'text that only a screen reader reads', html: `<span style="position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap">${word}</span>` },
  { name: 'a tight line height in a box with a fixed height', html: `<p style="width:100px;height:16px;margin:0;font-size:16px;line-height:16px">Text</p>` },
  { name: 'text that wraps in a box with a fixed height that scrolls', html: `<div style="width:100px;height:40px;overflow-y:auto">${words}</div>` },
  { name: 'text that is all left of its clip in a justify-end flex box', html: `<div style="display:flex;justify-content:flex-end;width:40px;overflow:hidden">Label<span style="flex:none;width:200px"></span></div>` },
  { name: 'text after a long page in a body with a fixed height', html: `<style>html,body{height:100%;margin:0}</style><div style="height:2000px"></div><span>Text</span>` },
  { name: 'a collapsed label that is all outside its clip', html: `<div style="width:40px;overflow:hidden;display:flex"><span style="margin-left:60px;white-space:nowrap">${word}</span></div>` },
  { name: 'prose that runs below the window, in a page that scrolls', html: `<p style="width:100px">${words.repeat(10)}</p>` },
  { name: 'a closed panel that is fixed all past the left of the window', html: `<div style="position:fixed;top:0;left:-300px;width:200px">Some words</div>` },
  { name: 'a closed panel that is fixed all past the right of the window', html: `<div style="position:fixed;top:0;left:100%;width:200px">Some words</div>` },
  { name: 'a closed panel that is fixed all past the top of the window', html: `<div style="position:fixed;top:-100px;left:0;height:50px">Some words</div>` },
  { name: 'a closed panel that is fixed all past the bottom of the window', html: `<div style="position:fixed;top:100%;left:0">Some words</div>` },
]

let browser
let page
let outDir
before(async () => {
  browser = await chromium.launch()
  page = await browser.newPage({ viewport: { width: 390, height: 844 } })
  outDir = mkdtempSync(path.join(tmpdir(), 'sweep-test-'))
})
after(async () => {
  await browser.close()
  rmSync(outDir, { recursive: true })
})

const faultsOf = async (html) => {
  await page.setContent(`<!doctype html><html><body>${html}</body></html>`)
  return page.evaluate(markLayoutFaults)
}

for (const { name, html, kind, element } of faults) {
  test(`reports ${name}`, async () => {
    const found = await faultsOf(html)
    assert.ok(found.some((fault) => fault.kind === kind && fault.element === element), JSON.stringify(found, null, 2))
  })
}

for (const { name, html } of planned) {
  test(`does not report ${name}`, async () => {
    assert.deepEqual(await faultsOf(html), [])
  })
}

test('reports a word that passes a heading and its parent once, as a fault of the heading', async () => {
  const found = await faultsOf(`<div style="width:100px"><h2 data-slot="dialog-title">${word.slice(0, 20)}</h2></div>`)
  assert.deepEqual(found.map(({ kind, element }) => ({ kind, element })), [{ kind: 'heading text runs past its box', element: 'dialog-title' }])
})

// Each test gets a new page, because a timer of the page in a test must not fire in the next test.
const focusFaultsAfterPress = async (onPress) => {
  const page = await browser.newPage()
  await page.setContent(`<!doctype html><html><body>
    <div role="dialog"><button>Join tenant</button></div>
    <h1 tabindex="-1">Home</h1>
    <script>document.querySelector('button').addEventListener('click', ({ target: button }) => { ${onPress} })</script>
  </body></html>`)
  await page.evaluate(trackLastFocused)
  await page.getByRole('button').click()
  const found = []
  await sweepFocus({ page, settle: async () => {}, outDir, step: 'press the button', faults: found })
  await page.close()
  return found.map(({ kind, how }) => ({ kind, how }))
}

const headingFocusAfter = (ms) => `setTimeout(() => document.querySelector('h1').focus(), ${ms})`
const removed = [{ kind: 'focus goes to the body', how: 'the focused element was removed' }]

test('reports a focus that stays on the body after the focused element is removed', async () => {
  assert.deepEqual(await focusFaultsAfterPress('button.remove()'), removed)
})

for (const ms of [500, 990]) {
  test(`does not report a focus that moves to the heading ${ms} ms after the focused element is removed`, async () => {
    assert.deepEqual(await focusFaultsAfterPress(`button.remove(); ${headingFocusAfter(ms)}`), [])
  })
}

test('reports a focus that moves to the heading 1500 ms after the focused element is removed', async () => {
  assert.deepEqual(await focusFaultsAfterPress(`button.remove(); ${headingFocusAfter(1500)}`), removed)
})

test('reports a focus that goes to the heading and returns to the body in a blur', async () => {
  assert.deepEqual(await focusFaultsAfterPress(`button.remove(); ${headingFocusAfter(600)}; setTimeout(() => document.querySelector('h1').blur(), 650)`), removed)
})

test('reports a focus that goes to the heading and returns to the body when the heading is removed with no blur', async () => {
  assert.deepEqual(await focusFaultsAfterPress(`button.remove(); ${headingFocusAfter(600)}; setTimeout(() => document.querySelector('h1').remove(), 650)`), removed)
})

test('reports a focus that stays on the body after the focused element becomes inert', async () => {
  assert.deepEqual(await focusFaultsAfterPress('button.parentElement.inert = true'), [{ kind: 'focus goes to the body', how: 'the focused element became inert' }])
})

test('reports a focus that stays on the body after the focused element is blurred and hidden', async () => {
  assert.deepEqual(await focusFaultsAfterPress(`button.blur(); button.style.display = 'none'`), [{ kind: 'focus goes to the body', how: 'the focused element was hidden' }])
})

test('reports a focus that stays on the body after the focused element gets visibility: hidden', async () => {
  assert.deepEqual(await focusFaultsAfterPress(`button.style.visibility = 'hidden'`), [{ kind: 'focus goes to the body', how: 'the focused element was hidden' }])
})

test('reports a focus that stays on the body after a dialog closes and returns no focus', async () => {
  assert.deepEqual(await focusFaultsAfterPress('button.parentElement.remove()'), removed)
})

test('reports a focus that stays on the body when the button that lost it is enabled again 600 ms later', async () => {
  assert.deepEqual(await focusFaultsAfterPress('button.disabled = true; setTimeout(() => { button.disabled = false }, 600)'), [{ kind: 'focus goes to the body', how: 'the focused element was disabled' }])
})
