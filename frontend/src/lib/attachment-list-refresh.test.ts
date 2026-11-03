import { QueryClient, QueryObserver } from '@tanstack/react-query'
import { afterEach, describe, expect, it, vi } from 'vitest'

import type { WorkshopAttachmentDto } from '@/api/generated/models/types/workshop-attachment-dto'

import { untilRefresh, workshopAttachmentsQueryOptions } from './get-workshop-attachments'

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

const loadedAt = Date.parse('2026-05-20T15:00:00Z')

describe('untilRefresh', () => {
  it('loads the list again a minute before the first URL expires, counted from the load', () => {
    const attachments = [expiringAt('2026-05-20T15:15:00Z'), expiringAt('2026-05-20T15:14:00Z')]

    expect(untilRefresh(attachments, loadedAt)).toBe(13 * 60_000)
  })

  it('loads a list whose URLs expire within the minute again in a second, not at once in a loop', () => {
    expect(untilRefresh([expiringAt('2026-05-20T15:00:30Z')], loadedAt)).toBe(1_000)
  })

  it('does not load an empty list again on a timer', () => {
    expect(untilRefresh([], loadedAt)).toBe(false)
    expect(untilRefresh(undefined, loadedAt)).toBe(false)
  })
})

describe('workshopAttachmentsQueryOptions', () => {
  afterEach(() => {
    vi.useRealTimers()
  })

  const isStaleAt = (now: number) => {
    vi.useFakeTimers({ now })
    const client = new QueryClient()
    const options = workshopAttachmentsQueryOptions('w_1')
    client.setQueryData(options.queryKey, [expiringAt('2026-05-20T15:15:00Z')], { updatedAt: loadedAt })
    return new QueryObserver(client, options).getCurrentResult().isStale
  }

  it('keeps the list fresh until a minute before the first URL expires, not half that time', () => {
    expect(isStaleAt(loadedAt + 13 * 60_000)).toBe(false)
    expect(isStaleAt(loadedAt + 14 * 60_000 + 1)).toBe(true)
  })
})
