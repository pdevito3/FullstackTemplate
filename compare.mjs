import { readdirSync, readFileSync, writeFileSync, mkdirSync } from 'node:fs'
import { createRequire } from 'node:module'

const require = createRequire(import.meta.url)
const { PNG } = require(process.env.PNGJS)

const [mainDir, headDir, outDir] = process.argv.slice(2)
mkdirSync(outDir, { recursive: true })
const gap = 16

for (const file of readdirSync(mainDir).filter((f) => f.endsWith('.png')).sort()) {
  const a = PNG.sync.read(readFileSync(`${mainDir}/${file}`))
  const b = PNG.sync.read(readFileSync(`${headDir}/${file}`))
  let diff = 0
  let box = null
  if (a.width === b.width && a.height === b.height) {
    for (let y = 0; y < a.height; y++) {
      for (let x = 0; x < a.width; x++) {
        const i = (y * a.width + x) * 4
        if (a.data[i] !== b.data[i] || a.data[i + 1] !== b.data[i + 1] || a.data[i + 2] !== b.data[i + 2]) {
          diff++
          box = box
            ? { x0: Math.min(box.x0, x), y0: Math.min(box.y0, y), x1: Math.max(box.x1, x), y1: Math.max(box.y1, y) }
            : { x0: x, y0: y, x1: x, y1: y }
        }
      }
    }
  }
  console.log(`${file}\t${diff === 0 ? 'identical' : `${diff} px differ, box ${JSON.stringify(box)}`}`)
  if (diff === 0) continue
  const out = new PNG({ width: a.width + gap + b.width, height: Math.max(a.height, b.height) })
  out.data.fill(255)
  PNG.bitblt(a, out, 0, 0, a.width, a.height, 0, 0)
  PNG.bitblt(b, out, 0, 0, b.width, b.height, a.width + gap, 0)
  writeFileSync(`${outDir}/${file}`, PNG.sync.write(out))
}
