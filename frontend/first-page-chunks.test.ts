import type { Rollup } from 'vite'
import { describe, expect, it } from 'vitest'

import { firstPageChunks } from './first-page-chunks'

type ChunkInput = { isEntry?: boolean; imports?: string[]; dynamicImports?: string[]; bytes?: number }

function bundleOf(chunks: Record<string, ChunkInput>): Rollup.OutputBundle {
  const outputs: Record<string, unknown> = {
    'index.css': { type: 'asset', fileName: 'index.css', source: 'x'.repeat(5000) },
  }
  for (const [fileName, chunk] of Object.entries(chunks)) {
    outputs[fileName] = {
      type: 'chunk',
      fileName,
      isEntry: chunk.isEntry ?? false,
      imports: chunk.imports ?? [],
      dynamicImports: chunk.dynamicImports ?? [],
      code: 'x'.repeat(chunk.bytes ?? 1000),
    }
  }
  return outputs as Rollup.OutputBundle
}

const namesOf = (bundle: Rollup.OutputBundle) => firstPageChunks(bundle).map((chunk) => chunk.fileName).sort()

describe('firstPageChunks', () => {
  it('counts the entry and each chunk that it imports, also through another chunk', () => {
    const bundle = bundleOf({
      'index.js': { isEntry: true, imports: ['react.js'] },
      'react.js': { imports: ['scheduler.js'] },
      'scheduler.js': {},
    })

    expect(namesOf(bundle)).toEqual(['index.js', 'react.js', 'scheduler.js'])
  })

  it('leaves out a chunk that loads only by a dynamic import, and the chunks that it imports', () => {
    const bundle = bundleOf({
      'index.js': { isEntry: true, dynamicImports: ['route.js'] },
      'route.js': { imports: ['date-picker.js'] },
      'date-picker.js': {},
    })

    expect(namesOf(bundle)).toEqual(['index.js'])
  })

  it('counts each chunk of an import cycle one time', () => {
    const bundle = bundleOf({
      'index.js': { isEntry: true, imports: ['a.js'] },
      'a.js': { imports: ['b.js'] },
      'b.js': { imports: ['a.js'] },
    })

    expect(namesOf(bundle)).toEqual(['a.js', 'b.js', 'index.js'])
  })

  it('gives the size of each chunk in kB of code, largest first, and leaves out assets and external imports', () => {
    const bundle = bundleOf({
      'index.js': { isEntry: true, imports: ['aria.js', 'https://cdn.example.com/x.js'], bytes: 2500 },
      'aria.js': { bytes: 542020 },
    })

    expect(firstPageChunks(bundle)).toEqual([
      { fileName: 'aria.js', sizeKb: 542.02 },
      { fileName: 'index.js', sizeKb: 2.5 },
    ])
  })
})
