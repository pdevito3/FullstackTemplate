// Usage: node diff.mjs <frontendDir> <dirA> <dirB>
// For each PNG in both dirs: the count of pixels that differ and their bounding box, measured in Chromium.
import { createRequire } from 'node:module'
import { readdirSync, readFileSync } from 'node:fs'
import path from 'node:path'

const [frontendDir, dirA, dirB] = process.argv.slice(2)
const require = createRequire(path.join(frontendDir, 'package.json'))
const { chromium } = require('@playwright/test')
const browser = await chromium.launch()
const page = await browser.newPage()

for (const name of readdirSync(dirA).filter((n) => n.endsWith('.png')).sort()) {
  const a = readFileSync(path.join(dirA, name)).toString('base64')
  const b = readFileSync(path.join(dirB, name)).toString('base64')
  const r = await page.evaluate(async ([a, b]) => {
    const load = async (src) => { const i = new Image(); i.src = `data:image/png;base64,${src}`; await i.decode(); return i }
    const [ia, ib] = await Promise.all([load(a), load(b)])
    if (ia.width !== ib.width || ia.height !== ib.height) return { size: `${ia.width}x${ia.height} vs ${ib.width}x${ib.height}` }
    const px = (img) => { const c = document.createElement('canvas'); c.width = img.width; c.height = img.height; const x = c.getContext('2d'); x.drawImage(img, 0, 0); return x.getImageData(0, 0, img.width, img.height).data }
    const da = px(ia), db = px(ib)
    let n = 0, x0 = 1e9, y0 = 1e9, x1 = -1, y1 = -1
    for (let i = 0; i < da.length; i += 4) {
      if (da[i] !== db[i] || da[i + 1] !== db[i + 1] || da[i + 2] !== db[i + 2]) {
        n++; const p = i / 4, x = p % ia.width, y = Math.floor(p / ia.width)
        x0 = Math.min(x0, x); y0 = Math.min(y0, y); x1 = Math.max(x1, x); y1 = Math.max(y1, y)
      }
    }
    return n ? { n, box: [x0, y0, x1, y1] } : { n }
  }, [a, b])
  console.log(name.padEnd(40), JSON.stringify(r))
}
await browser.close()
