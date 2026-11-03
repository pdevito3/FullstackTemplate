import { describe, expect, it } from 'vitest'

import type { WorkshopAttachmentDto } from '@/api/generated/models/types/workshop-attachment-dto'

import { untilRefresh } from './get-workshop-attachments'

const expiringAt = (urlsExpireAt: string): WorkshopAttachmentDto => ({
  id: urlsExpireAt,
  fileName: 'notes.txt',
  contentType: 'text/plain',
  sizeBytes: 10,
  uploadedAt: '2026-05-20T15:00:00Z',
  downloadUrl: 'https://storage.test/notes',
  previewUrl: null,
  urlsExpireAt,
})

const now = Date.parse('2026-05-20T15:00:00Z')

describe('untilRefresh', () => {
  it('loads the list again a minute before the first URL expires', () => {
    const attachments = [expiringAt('2026-05-20T15:15:00Z'), expiringAt('2026-05-20T15:14:00Z')]

    expect(untilRefresh(attachments, now)).toBe(13 * 60_000)
  })

  it('loads a list whose URLs expire within the minute again in a second, not at once in a loop', () => {
    expect(untilRefresh([expiringAt('2026-05-20T15:00:30Z')], now)).toBe(1_000)
  })

  it('does not load an empty list again on a timer', () => {
    expect(untilRefresh([], now)).toBe(false)
    expect(untilRefresh(undefined, now)).toBe(false)
  })
})
