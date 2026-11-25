import type { Rollup } from 'vite'

export type ChunkSize = { fileName: string; sizeKb: number }

type ChunkFields = Pick<Rollup.OutputChunk, 'type' | 'fileName' | 'isEntry' | 'imports' | 'code'>

/** The fields of each output of a bundle that firstPageChunks reads. A `Rollup.OutputBundle` has them. */
export type BundleFields = Record<string, Pick<Rollup.OutputAsset, 'type'> | ChunkFields>

export function firstPageChunks(bundle: BundleFields): ChunkSize[] {
  const chunks = new Map(
    Object.values(bundle)
      .filter((output) => output.type === 'chunk')
      .map((chunk) => [chunk.fileName, chunk]),
  )

  const loaded = new Set<ChunkFields>()

  const visit = (chunk: ChunkFields | undefined) => {
    if (!chunk || loaded.has(chunk)) return
    loaded.add(chunk)
    chunk.imports.forEach((fileName) => visit(chunks.get(fileName)))
  }

  for (const chunk of chunks.values()) if (chunk.isEntry) visit(chunk)

  return [...loaded]
    .map((chunk) => ({ fileName: chunk.fileName, sizeKb: Buffer.byteLength(chunk.code) / 1000 }))
    .sort((a, b) => b.sizeKb - a.sizeKb)
}
