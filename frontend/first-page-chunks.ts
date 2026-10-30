import type { Rollup } from 'vite'

export type ChunkSize = { fileName: string; sizeKb: number }

export function firstPageChunks(bundle: Rollup.OutputBundle): ChunkSize[] {
  const chunks = new Map(
    Object.values(bundle)
      .filter((output) => output.type === 'chunk')
      .map((chunk) => [chunk.fileName, chunk]),
  )
  const loaded = new Set<Rollup.OutputChunk>()
  const visit = (chunk: Rollup.OutputChunk | undefined) => {
    if (!chunk || loaded.has(chunk)) return
    loaded.add(chunk)
    chunk.imports.forEach((fileName) => visit(chunks.get(fileName)))
  }
  for (const chunk of chunks.values()) if (chunk.isEntry) visit(chunk)
  return [...loaded]
    .map((chunk) => ({ fileName: chunk.fileName, sizeKb: Buffer.byteLength(chunk.code) / 1000 }))
    .sort((a, b) => b.sizeKb - a.sizeKb)
}
