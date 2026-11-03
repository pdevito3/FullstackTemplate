import { QueryClient, QueryObserver } from '@tanstack/react-query'
import { afterEach, expect, it, vi } from 'vitest'

import type { WorkshopAttachmentDto } from '@/api/generated/models/types/workshop-attachment-dto'

import { workshopAttachmentsQueryOptions } from './get-workshop-attachments'

// Query Core starts a refetch interval only when a window exists, so this test runs in the browser project

const loadedAt = Date.parse('2026-05-20T15:00:00Z')

const expiringAt = (urlsExpireAt: string): WorkshopAttachmentDto => ({
  id: 'wkatt_1',
  fileName: 'notes.txt',
  contentType: 'text/plain',
  sizeBytes: 10,
  uploadedAt: '2026-05-20T15:00:00Z',
  downloadUrl: 'https://storage.test/notes',
  previewUrl: null,
  urlsExpireAt,
})

afterEach(() => {
  vi.useRealTimers()
})

it('loads the list again before the URLs expire when a page subscribes some minutes after the load', async () => {
  vi.useFakeTimers({ now: loadedAt })
  const client = new QueryClient()
  const options = workshopAttachmentsQueryOptions('w_1')
  client.setQueryData(options.queryKey, [expiringAt('2026-05-20T15:15:00Z')], { updatedAt: loadedAt })
  const queryFn = vi.fn().mockResolvedValue([expiringAt('2026-05-20T15:30:00Z')])

  await vi.advanceTimersByTimeAsync(4 * 60_000)
  const unsubscribe = new QueryObserver(client, { ...options, queryFn }).subscribe(() => {})
  await vi.advanceTimersByTimeAsync(10 * 60_000 - 1)
  expect(queryFn).not.toHaveBeenCalled()
  await vi.advanceTimersByTimeAsync(1)

  expect(queryFn).toHaveBeenCalledTimes(1)
  unsubscribe()
})
