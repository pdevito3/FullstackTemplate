// Tests the layout checks of the sweep on small pages in a real Chromium. The test needs no stack.
//
// Usage: node --test .agents/skills/verify-fullstack-template/scripts/sweep.test.mjs
import assert from 'node:assert/strict'
import { createRequire } from 'node:module'
import { after, before, test } from 'node:test'
import { markLayoutFaults } from './sweep.mjs'

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
  { name: 'a line clamp in a link', html: `<a href="/" style="${lineClamp}">${words}</a>` },
  { name: 'a line clamp on a parent, with the text in an inline child', html: `<a href="/" style="${lineClamp}"><span>${words}</span></a>` },
  { name: 'a long word that wraps', html: `<div style="width:100px;overflow-wrap:anywhere">${word}</div>` },
  { name: 'a wide table in a box that scrolls sideways', html: `<div style="width:100px;overflow-x:auto"><table><tr><td style="white-space:nowrap">${word}</td></tr></table></div>` },
  { name: 'a line of code that scrolls sideways', html: `<pre style="width:100px;overflow-x:auto"><code>${word}</code></pre>` },
  { name: 'a line that does not wrap in a box that scrolls sideways', html: `<div style="width:100px;overflow-x:auto"><p style="white-space:nowrap">${word}</p></div>` },
  { name: 'an inline child that does not wrap in a box that scrolls sideways', html: `<div style="width:100px;overflow-x:auto"><p><span style="white-space:nowrap">${word}</span></p></div>` },
  { name: 'a separator that hangs past its box', html: `<div style="width:4px"><span style="white-space:nowrap;padding:0 6px">|</span></div>` },
  { name: 'text that only a screen reader reads', html: `<span style="position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0,0,0,0);white-space:nowrap">${word}</span>` },
  { name: 'a collapsed label that is all outside its clip', html: `<div style="width:40px;overflow:hidden;display:flex"><span style="margin-left:60px;white-space:nowrap">${word}</span></div>` },
]

let browser
let page
before(async () => {
  browser = await chromium.launch()
  page = await browser.newPage({ viewport: { width: 390, height: 844 } })
})
after(() => browser.close())

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
