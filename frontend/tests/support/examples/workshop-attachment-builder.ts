import type { WorkshopAttachmentDto } from '@/api/generated/models/types/workshop-attachment-dto'

let seed = 0
const next = () => ++seed

// The Vite dev server serves this image, so a thumbnail loads with no storage behind it
const IMAGE_URL = '/Aspire.png'
const PREVIEW_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp']

class WorkshopAttachmentBuilder {
  private readonly attachment: WorkshopAttachmentDto

  private constructor(attachment: WorkshopAttachmentDto) {
    this.attachment = attachment
  }

  static create() {
    const n = next()
    return new WorkshopAttachmentBuilder({
      id: `00000000-0000-4000-a000-${String(n).padStart(12, '0')}`,
      fileName: `notes-${n}.pdf`,
      contentType: 'application/pdf',
      sizeBytes: 24_576,
      uploadedAt: '2026-05-20T15:00:00+00:00',
      downloadUrl: `https://storage.test/files/${n}?download`,
      previewUrl: null,
      urlsExpireAt: '2099-01-01T00:00:00+00:00',
    })
  }

  // Like the Server, only an image of a preview type gets a preview URL
  named(fileName: string, contentType: string) {
    const previewUrl = PREVIEW_TYPES.includes(contentType) ? IMAGE_URL : null
    return new WorkshopAttachmentBuilder({ ...this.attachment, fileName, contentType, previewUrl })
  }
  ofSize(sizeBytes: number) {
    return new WorkshopAttachmentBuilder({ ...this.attachment, sizeBytes })
  }

  build(): WorkshopAttachmentDto {
    return this.attachment
  }
}

export const aWorkshopAttachment = () => WorkshopAttachmentBuilder.create()
