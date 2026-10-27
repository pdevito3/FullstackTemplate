// Usage: node match-any.mjs <before dirs, comma separated> <after dirs, comma separated>
// Passes when each after screenshot has the exact pixels of the same screenshot in at least one before run.
import { readFileSync, readdirSync } from 'node:fs'
import { createRequire } from 'node:module'
const { PNG } = createRequire(process.env.PNGJS_FROM)('pngjs')
const [before, after] = process.argv.slice(2).map((s) => s.split(','))
const pixels = (path) => { const p = PNG.sync.read(readFileSync(path)); return `${p.width}x${p.height}:` + p.data.toString('base64') }
let bad = 0, checked = 0
for (const dir of after) {
  for (const name of readdirSync(dir).filter((n) => n.endsWith('.png'))) {
    checked++
    const mine = pixels(`${dir}/${name}`)
    if (!before.some((b) => { try { return pixels(`${b}/${name}`) === mine } catch { return false } })) {
      console.log(`NO EXACT MATCH ${dir}/${name}`); bad++
    }
  }
}
const names = new Set(before.flatMap((b) => readdirSync(b)))
console.log(`${checked} after screenshots, ${names.size} names, ${bad} without an exact before match`)
process.exit(bad === 0 && checked > 0 ? 0 : 1)
