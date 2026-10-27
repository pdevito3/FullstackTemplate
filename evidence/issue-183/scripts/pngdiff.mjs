// Usage: node pngdiff.mjs <dirA> <dirB>. Prints per file: identical, or the count and bounding box of differing pixels.
import { readFileSync, readdirSync, existsSync } from 'node:fs'
import { createRequire } from 'node:module'
const require = createRequire(process.env.PNGJS_FROM)
const { PNG } = require('pngjs')
const [a, b] = process.argv.slice(2)
let bad = 0
for (const name of readdirSync(a).filter((n) => n.endsWith('.png')).sort()) {
  if (!existsSync(`${b}/${name}`)) { console.log(`MISSING ${name}`); bad++; continue }
  const x = PNG.sync.read(readFileSync(`${a}/${name}`))
  const y = PNG.sync.read(readFileSync(`${b}/${name}`))
  if (x.width !== y.width || x.height !== y.height) { console.log(`SIZE ${name} ${x.width}x${x.height} vs ${y.width}x${y.height}`); bad++; continue }
  let n = 0, minX = 1e9, minY = 1e9, maxX = -1, maxY = -1
  for (let i = 0; i < x.data.length; i += 4) {
    if (x.data[i] !== y.data[i] || x.data[i + 1] !== y.data[i + 1] || x.data[i + 2] !== y.data[i + 2] || x.data[i + 3] !== y.data[i + 3]) {
      n++; const p = i / 4, px = p % x.width, py = Math.floor(p / x.width)
      minX = Math.min(minX, px); maxX = Math.max(maxX, px); minY = Math.min(minY, py); maxY = Math.max(maxY, py)
    }
  }
  if (n === 0) console.log(`identical ${name}`)
  else { console.log(`PIXELS ${name} ${n} px in x ${minX}-${maxX} y ${minY}-${maxY}`); bad++ }
}
console.log(bad === 0 ? 'ALL IDENTICAL' : `${bad} DIFFER`)
process.exit(bad === 0 ? 0 : 1)
