import type { AttachmentDto } from '@/api/generated/models/types/attachment-dto'

let seed = 0
const next = () => ++seed

// Vite serves this image from public/, so a thumbnail loads with no storage behind it
const IMAGE_URL = '/Aspire.png'

class WorkshopAttachmentBuilder {
  private readonly attachment: AttachmentDto

  private constructor(attachment: AttachmentDto) {
    this.attachment = attachment
  }

  static create() {
    const n = next()
    return new WorkshopAttachmentBuilder(
      signed({
        id: `00000000-0000-4000-a000-${String(n).padStart(12, '0')}`,
        fileName: `notes-${n}.pdf`,
        mediaType: 'application/pdf',
        sizeBytes: 24_576,
        uploadedAt: '2026-05-20T15:00:00+00:00',
        urlsExpireAt: '2099-01-01T00:00:00+00:00',
      }),
    )
  }

  named(fileName: string, mediaType: string) {
    return new WorkshopAttachmentBuilder(signed({ ...this.attachment, fileName, mediaType }))
  }
  // The Server decides which types show in the page, so the builder copies no list of them
  withPreview() {
    return new WorkshopAttachmentBuilder(signed(this.attachment, { preview: true }))
  }
  signedUntil(urlsExpireAt: string) {
    return new WorkshopAttachmentBuilder(signed({ ...this.attachment, urlsExpireAt }))
  }
  ofSize(sizeBytes: number) {
    return new WorkshopAttachmentBuilder({ ...this.attachment, sizeBytes })
  }

  build(): AttachmentDto {
    return this.attachment
  }
}

function signed(
  attachment: Omit<AttachmentDto, 'downloadUrl' | 'previewUrl'> & Partial<Pick<AttachmentDto, 'previewUrl'>>,
  { preview = attachment.previewUrl != null } = {},
): AttachmentDto {
  const signature = `expires=${encodeURIComponent(attachment.urlsExpireAt)}`
  return {
    ...attachment,
    downloadUrl: `https://storage.test/files/${attachment.id}?download&${signature}`,
    previewUrl: preview ? `${IMAGE_URL}?${signature}` : null,
  }
}

export const aWorkshopAttachment = () => WorkshopAttachmentBuilder.create()
