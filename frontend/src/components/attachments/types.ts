// The fields that the attachment components read. The AttachmentDto and AttachmentListDto of each use have them. The
// components do not import the generated types, because Kubb makes them only when an endpoint of the Server returns
// them, and the components stay in an app with no attachments yet.

export interface Attachment {
  id: string
  fileName: string
  sizeBytes: number
  uploadedAt: string
  downloadUrl: string
  previewUrl?: string | null
  urlsExpireAt: string
}

export interface FileType {
  name: string
  mediaType: string
  extensions: string[]
}

export interface UploadRules {
  fileTypes: FileType[]
  maxSizeBytes: number
}

export interface AttachmentList {
  uploadRules: UploadRules
  attachments: Attachment[]
}

/** The variables of an upload mutation. The Cancel button of the Files section aborts the signal. */
export interface Upload {
  file: File
  signal: AbortSignal
}
