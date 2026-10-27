// node diff.cjs before/shots after/shots: size and differing-pixel bounding box of each screenshot pair.
const fs = require('node:fs'), path = require('node:path')
const { PNG } = require('/Users/paul/repos/FullstackTemplate-worktrees/issue-145/frontend/node_modules/.pnpm/pngjs@7.0.0/node_modules/pngjs')
const [a, b] = process.argv.slice(2)
for (const f of fs.readdirSync(a).sort()) {
  const x = PNG.sync.read(fs.readFileSync(path.join(a, f))), y = PNG.sync.read(fs.readFileSync(path.join(b, f)))
  if (x.width !== y.width || x.height !== y.height) { console.log(f, 'SIZE', x.width, x.height, '->', y.width, y.height); continue }
  let n = 0, box = [Infinity, Infinity, -1, -1]
  for (let i = 0; i < x.data.length; i += 4) if (x.data[i] !== y.data[i] || x.data[i + 1] !== y.data[i + 1] || x.data[i + 2] !== y.data[i + 2]) {
    n++; const p = i / 4, px = p % x.width, py = Math.floor(p / x.width)
    box = [Math.min(box[0], px), Math.min(box[1], py), Math.max(box[2], px), Math.max(box[3], py)]
  }
  console.log(f, `${x.width}x${x.height}`, n ? `diff ${n}px in x${box[0]}-${box[2]} y${box[1]}-${box[3]}` : 'identical')
}
