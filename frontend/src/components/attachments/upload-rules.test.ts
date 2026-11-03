import { describe, expect, it } from 'vitest'

import type { UploadRules } from './types'
import { acceptedFileTypes, uploadHint } from './upload-rules'

const png = { name: 'PNG', mediaType: 'image/png', extensions: ['.png'] }
const jpeg = { name: 'JPEG', mediaType: 'image/jpeg', extensions: ['.jpg', '.jpeg'] }
const pdf = { name: 'PDF', mediaType: 'application/pdf', extensions: ['.pdf'] }

describe('uploadHint', () => {
  it.each<[UploadRules, string]>([
    [{ fileTypes: [pdf], maxSizeBytes: 2 * 1024 * 1024 }, 'PDF. Up to 2 MB.'],
    [{ fileTypes: [png, pdf], maxSizeBytes: 512 * 1024 }, 'PNG or PDF. Up to 512 kB.'],
    [{ fileTypes: [png, jpeg, pdf], maxSizeBytes: 25 * 1024 * 1024 }, 'PNG, JPEG, or PDF. Up to 25 MB.'],
  ])('names the types and the size of the rules', (rules, hint) => {
    expect(uploadHint(rules, 'en-US')).toBe(hint)
  })
})

describe('acceptedFileTypes', () => {
  it('lets the chooser match a file by each extension and by the media type of each type', () => {
    expect(acceptedFileTypes({ fileTypes: [png, jpeg], maxSizeBytes: 1 })).toEqual([
      '.png',
      'image/png',
      '.jpg',
      '.jpeg',
      'image/jpeg',
    ])
  })
})
